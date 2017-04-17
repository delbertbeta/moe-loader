using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using HtmlAgilityPack;
using MoeLoader;

namespace SitePack
{
    public class SitePixiv : MoeLoader.AbstractImageSite
    {
        public enum PixivSrcType { Tag, Author, Day, Week, Month, }

        public override string SiteUrl { get { return "http://www.pixiv.net"; } }
        public override string SiteName
        {
            get
            {
                if (srcType == PixivSrcType.Author)
                    return "www.pixiv.net [User]";
                else if (srcType == PixivSrcType.Day)
                    return "www.pixiv.net [Day]";
                else if (srcType == PixivSrcType.Week)
                    return "www.pixiv.net [Week]";
                else if (srcType == PixivSrcType.Month)
                    return "www.pixiv.net [Month]";
                else return "www.pixiv.net [Tag]";
            }
        }
        public override string ToolTip
        {
            get
            {
                if (srcType == PixivSrcType.Author)
                    return "作者搜索";
                else if (srcType == PixivSrcType.Day)
                    return "本日排行";
                else if (srcType == PixivSrcType.Week)
                    return "本周排行";
                else if (srcType == PixivSrcType.Month)
                    return "本月排行";
                else return "最新作品 & 标签搜索";
            }
        }
        public override string ShortName { get { return "pixiv"; } }
        public override string Referer { get { return "http://www.pixiv.net/"; } }

        public override bool IsSupportCount { get { return false; } } //fixed 20
        //public override bool IsSupportScore { get { return false; } }
        public override bool IsSupportRes { get { return false; } }
        //public override bool IsSupportPreview { get { return true; } }
        //public override bool IsSupportTag { get { if (srcType == PixivSrcType.Author) return true; else return false; } }
        public override bool IsSupportTag { get { return false; } }

        //public override System.Drawing.Point LargeImgSize { get { return new System.Drawing.Point(150, 150); } }
        //public override System.Drawing.Point SmallImgSize { get { return new System.Drawing.Point(150, 150); } }

        private string cookie = null;
        private string[] user = { "moe1user", "moe3user" };
        private string[] pass = { "630489372", "1515817701" };
        private Random rand = new Random();
        private PixivSrcType srcType = PixivSrcType.Tag;

        /// <summary>
        /// pixiv.net site
        /// </summary>
        public SitePixiv(PixivSrcType srcType)
        {
            this.srcType = srcType;
        }

        public override string GetPageString(int page, int count, string keyWord, System.Net.IWebProxy proxy)
        {
            //if (page > 1000) throw new Exception("页码过大，若需浏览更多图片请使用关键词限定范围");
            Login(proxy);

            //http://www.pixiv.net/new_illust.php?p=2
            string url = SiteUrl + "/new_illust.php?p=" + page;

            MyWebClient web = new MyWebClient();
            web.Proxy = proxy;
            web.Headers["Cookie"] = cookie;
            web.Encoding = Encoding.UTF8;

            if (keyWord.Length > 0)
            {
                //http://www.pixiv.net/search.php?s_mode=s_tag&word=hatsune&order=date_d&p=2
                url = SiteUrl + "/search.php?s_mode=s_tag&word=" + keyWord + "&order=date_d&p=" + page;
            }
            if (srcType == PixivSrcType.Author)
            {
                int memberId = 0;
                if (keyWord.Trim().Length == 0 || !int.TryParse(keyWord.Trim(), out memberId))
                {
                    throw new Exception("必须在关键词中指定画师 id；若需要使用标签进行搜索请使用 www.pixiv.net [TAG]");
                }
                //member id
                url = SiteUrl + "/member_illust.php?id=" + memberId + "&p=" + page;
            }
            else if (srcType == PixivSrcType.Day)
            {
                url = SiteUrl + "/ranking.php?mode=daily&p=" + page;
            }
            else if (srcType == PixivSrcType.Week)
            {
                url = SiteUrl + "/ranking.php?mode=weekly&p=" + page;
            }
            else if (srcType == PixivSrcType.Month)
            {
                url = SiteUrl + "/ranking.php?mode=monthly&p=" + page;
            }

            string pageString = web.DownloadString(url);
            web.Dispose();

            return pageString;
        }

        public override List<Img> GetImages(string pageString, System.Net.IWebProxy proxy)
        {
            List<Img> imgs = new List<Img>();

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(pageString);

            //retrieve all elements via xpath
            HtmlNodeCollection nodes = null;
            if (srcType == PixivSrcType.Tag)
            {
                nodes = doc.DocumentNode.SelectSingleNode("//ul[@class='_image-items autopagerize_page_element']").SelectNodes("li");
                //nodes = nodes[nodes.Count - 1].SelectNodes("li");
            }
            else if (srcType == PixivSrcType.Author)
            {
                nodes = doc.DocumentNode.SelectSingleNode("//ul[@class='_image-items']").SelectNodes("li");
            }
            //else if (srcType == PixivSrcType.Day || srcType == PixivSrcType.Month || srcType == PixivSrcType.Week) //ranking
            //nodes = doc.DocumentNode.SelectSingleNode("//section[@class='ranking-items autopagerize_page_element']").SelectNodes("div");
            else
            {
                //ranking
                nodes = doc.DocumentNode.SelectNodes("//section[@class='ranking-item']");
            }

            if (nodes == null)
            {
                return imgs;
            }

            foreach (HtmlNode imgNode in nodes)
            {
                try
                {
                    HtmlNode anode = imgNode.SelectSingleNode("a");
                    if (srcType == PixivSrcType.Day || srcType == PixivSrcType.Month || srcType == PixivSrcType.Week)
                    {
                        anode = imgNode.SelectSingleNode(".//div[@class='ranking-image-item']").SelectSingleNode("a");
                    }
                    //details will be extracted from here
                    //eg. member_illust.php?mode=medium&illust_id=29561307&ref=rn-b-5-thumbnail
                    string detailUrl = anode.Attributes["href"].Value.Replace("amp;", "");
                    string previewUrl = null;

                    //P站不直接把预览图地址放在src里面了，所以注释掉原来的几行判断代码。
                    //if (srcType == PixivSrcType.Tag || srcType == PixivSrcType.Author)
                    //    previewUrl = anode.SelectSingleNode(".//img").Attributes["src"].Value;
                    //else
                    previewUrl = anode.SelectSingleNode(".//img").Attributes["data-src"].Value;

                    if (previewUrl.Contains('?'))
                        previewUrl = previewUrl.Substring(0, previewUrl.IndexOf('?'));

                    //extract id from detail url
                    //string id = detailUrl.Substring(detailUrl.LastIndexOf('=') + 1);
                    string id = System.Text.RegularExpressions.Regex.Match(detailUrl, @"illust_id=\d+").Value;
                    id = id.Substring(id.IndexOf('=') + 1);

                    Img img = GenerateImg(detailUrl, previewUrl, id);
                    if (img != null) imgs.Add(img);
                }
                catch
                {
                    //int i = 0;
                }
            }

            return imgs;
        }

        // DO NOT SUPPORT TAG HINT
        //public override List<TagItem> GetTags(string word, System.Net.IWebProxy proxy)
        //{
        //    List<TagItem> re = new List<TagItem>();
        //    return re;
        //}

        private Img GenerateImg(string detailUrl, string preview_url, string id)
        {
            int intId = int.Parse(id);

            if (!detailUrl.StartsWith("http") && !detailUrl.StartsWith("/"))
                detailUrl = "/" + detailUrl;

            //convert relative url to absolute
            if (detailUrl.StartsWith("/"))
                detailUrl = SiteUrl + detailUrl;
            if (preview_url.StartsWith("/"))
                preview_url = SiteUrl + preview_url;

            //string fileUrl = preview_url.Replace("_s.", ".");
            //string sampleUrl = preview_url.Replace("_s.", "_m.");

            //http://i1.pixiv.net/img-inf/img/2013/04/10/00/11/37/34912478_s.png
            //http://i1.pixiv.net/img03/img/tukumo/34912478_m.png
            //http://i1.pixiv.net/img03/img/tukumo/34912478.png

            Img img = new Img()
            {
                //Date = "N/A",
                //FileSize = file_size.ToUpper(),
                //Desc = intId + " ",
                Id = intId,
                //JpegUrl = fileUrl,
                //OriginalUrl = fileUrl,
                PreviewUrl = preview_url,
                //SampleUrl = sampleUrl,
                //Score = 0,
                //Width = width,
                //Height = height,
                //Tags = tags,
                DetailUrl = detailUrl
            };

            img.DownloadDetail = new DetailHandler((i, p) =>
            {
                //retrieve details
                MyWebClient web = new MyWebClient();
                web.Proxy = p;
                web.Encoding = Encoding.UTF8;
                web.Headers["Cookie"] = cookie;
                web.Headers["Referer"] = Referer;
                string page = web.DownloadString(i.DetailUrl);

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(page);

                //2017年3月22日 19:43｜2400×1078｜SAI  or 04/16/2012 17:44｜600×800 or 04/19/2012 22:57｜漫画 6P｜SAI
                i.Date = doc.DocumentNode.SelectSingleNode("//ul[@class='meta']/li[1]").InnerText;
                //总点数
                i.Score = int.Parse(doc.DocumentNode.SelectSingleNode("//dd[@class='view-count']").InnerText);
                //「カルタ＆わたぬき」/「えれっと」のイラスト [pixiv]
                i.Desc += doc.DocumentNode.SelectSingleNode("//title").InnerText.Replace("のイラスト [pixiv]", "").Replace("の漫画 [pixiv]", "").Replace("「", "").Replace("」", "").Replace("/", "_");
                //URLS
                //http://i2.pixiv.net/c/600x600/img-master/img/2014/10/08/06/13/30/46422743_p0_master1200.jpg
                //http://i2.pixiv.net/img-original/img/2014/10/08/06/13/30/46422743_p0.png
                i.SampleUrl = doc.DocumentNode.SelectSingleNode("//div[@class='works_display']").SelectSingleNode(".//img").Attributes["src"].Value;
                try
                {
                    i.OriginalUrl = doc.DocumentNode.SelectSingleNode("//img[@class='original-image']").Attributes["data-src"].Value;
                    i.JpegUrl = i.OriginalUrl;
                }
                catch { }

                //600×800 or 漫画 6P
                string dimension = doc.DocumentNode.SelectSingleNode("//ul[@class='meta']/li[2]").InnerText;
                try
                {
                    //706×1000
                    i.Width = int.Parse(dimension.Substring(0, dimension.IndexOf('×')));
                    i.Height = int.Parse(System.Text.RegularExpressions.Regex.Match(dimension.Substring(dimension.IndexOf('×') + 1), @"\d+").Value);
                }
                catch { }
                //try
                //{
                if (i.Width == 0 && i.Height == 0)
                {
                    // http://i2.pixiv.net/img-original/img/2017/03/21/22/14/40/62031197_p1.jpg
                    // http://i2.pixiv.net/img-original/img/2017/03/21/22/14/40/62031197_p0.jpg
                    //i.OriginalUrl = i.SampleUrl.Replace("600x600", "1200x1200");
                    i.OriginalUrl = i.SampleUrl;
                    i.JpegUrl = i.OriginalUrl;
                    //manga list
                    //漫画 6P
                    int index = dimension.IndexOf(' ') + 1;
                    string mangaPart = dimension.Substring(index, dimension.IndexOf('P') - index);
                    int mangaCount = int.Parse(mangaPart);
                    i.Dimension = "Manga " + mangaCount + "P";
                    string url = "http://www.pixiv.net/" + doc.DocumentNode.SelectSingleNode("//div[@class='works_display']/a").Attributes["href"].Value;
                    getMangaUrlList(url, ref i, p);
                }
                //}
                //catch {
                //    System.Diagnostics.Debugger.Break();
                //}
            });

            return img;
        }

        private void getMangaUrlList(string url, ref Img img, System.Net.IWebProxy p)
        {
            List<string> urlList = new List<string>();
            List<string> detailUrlList = new List<string>();
            MyWebClient web = new MyWebClient();
            web.Proxy = p;
            web.Encoding = Encoding.UTF8;
            web.Headers["Cookie"] = cookie;
            web.Headers["Referer"] = Referer;
            string page = web.DownloadString(url);

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(page);

            var mangaPreviewNodes = doc.DocumentNode.SelectNodes("//a[@class='full-size-container _ui-tooltip']");
            foreach (var mangaPreviewNode in mangaPreviewNodes)
            {
                string detailPageUrl = "http://www.pixiv.net" + mangaPreviewNode.Attributes["href"].Value;
                detailUrlList.Add(detailPageUrl);
                string detailPage = web.DownloadString(detailPageUrl);
                int urlStartIndex = detailPage.IndexOf("src=") + 5;
                int urlEndIndex = detailPage.IndexOf("\"", urlStartIndex);
                var imgUrl = detailPage.Substring(urlStartIndex, urlEndIndex - urlStartIndex);
                urlList.Add(imgUrl);
            }

            img.OrignalUrlList = urlList;
            img.DetailUrlList = detailUrlList;
        }

        //private List<string> GetMangePics(int illustId)
        //{

        //}

        private void Login(System.Net.IWebProxy proxy)
        {
            if (cookie == null)
            {
                //获取SessionId和post_key
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("https://accounts.pixiv.net/login?lang=zh&source=pc&view_type=page&ref=wwwtop_accounts_index");
                var rsp = (System.Net.HttpWebResponse)req.GetResponse();
                var cookieContainer = new System.Net.CookieContainer();
                cookieContainer.SetCookies(rsp.ResponseUri, rsp.Headers["Set-Cookie"]);

                //这里是失败的尝试……跳过就好
                /*
                //cookieContainer.Add(phpsessidCookie);
                //cookieContainer.Add(p_ab_idCookie);
                cookie = rsp.Headers.Get("Set-Cookie");
                if (cookie == null)
                {
                    throw new Exception("获取初始PHPSESSID失败");
                }
                //Set-Cookie: PHPSESSID=3af0737dc5d8a27f5504a7b8fe427286; expires=Tue, 15-May-2012 10:05:39 GMT; path=/; domain=.pixiv.net
                int sessionIndex = cookie.LastIndexOf("PHPSESSID");
                string phpsessid = cookie.Substring(sessionIndex + 10, cookie.IndexOf(';', sessionIndex + 10) - sessionIndex - 9);
                int p_ab_idIndex = cookie.LastIndexOf("p_ab_id");
                string p_ab_id = cookie.Substring(p_ab_idIndex + 8, cookie.IndexOf(';', p_ab_idIndex + 8) - p_ab_idIndex - 8);
                rsp.Close();
                var phpsessidCookie = new System.Net.Cookie("p_ab_id", p_ab_id);
                var p_ab_idCookie = new System.Net.Cookie("PHPSESSID", phpsessid);
                */

                var loginPageStream = rsp.GetResponseStream();
                System.IO.StreamReader streamReader = new System.IO.StreamReader(loginPageStream);
                string loginPageString = streamReader.ReadToEnd();
                streamReader.Dispose();
                loginPageStream.Dispose();
                rsp.Close();

                HtmlDocument loginPage = new HtmlDocument();
                loginPage.LoadHtml(loginPageString);
                HtmlNode postKeyNode = loginPage.DocumentNode.SelectSingleNode("//input[@name='post_key']");
                string postKey = postKeyNode.Attributes.Where(p => p.Name == "value").Select(p => p.Value).ToList()[0];


                //模拟登陆
                req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("https://accounts.pixiv.net/api/login?lang=zh");
                req.UserAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0)";
                req.Proxy = proxy;
                req.Timeout = 8000;
                req.Method = "POST";
                //prevent 302
                req.AllowAutoRedirect = false;
                //user & pass
                int index = rand.Next(0, user.Length);
                string data = "captcha=&g_recaptcha_response=&pixiv_id=" + user[index] + "&password=" + pass[index] + "&source=pc" + "&post_key=" + postKey + "&ref=wwwtop_accounts_index" + "&return_to=http%3A%2F%2Fwww.pixiv.net%2F";
                byte[] buf = Encoding.UTF8.GetBytes(data);
                req.ContentLength = buf.Length;
                req.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                req.KeepAlive = true;
                req.Referer = "https://accounts.pixiv.net/login?lang=zh&source=pc&view_type=page&ref=wwwtop_accounts_index";
                req.Accept = "application/json, text/javascript, */*; q=0.01";
                req.CookieContainer = cookieContainer;


                System.IO.Stream str = req.GetRequestStream();
                str.Write(buf, 0, buf.Length);
                str.Close();
                var getRsp = req.GetResponse();

                //HTTP 302然后返回实际地址
                cookie = getRsp.Headers.Get("Set-Cookie");
                if (/*rsp.Headers.Get("Location") == null ||*/ cookie == null)
                {
                    throw new Exception("自动登录失败");
                }
                //Set-Cookie: PHPSESSID=3af0737dc5d8a27f5504a7b8fe427286; expires=Tue, 15-May-2012 10:05:39 GMT; path=/; domain=.pixiv.net
                int sessionIndex = cookie.LastIndexOf("PHPSESSID");
                int device_tokenIndex = cookie.LastIndexOf("device_token");
                cookie = cookie.Substring(sessionIndex, cookie.IndexOf(';', sessionIndex) - sessionIndex) + "; " + cookie.Substring(device_tokenIndex, cookie.IndexOf(';', device_tokenIndex) - device_tokenIndex);
                rsp.Close();
            }
        }
    }
}
