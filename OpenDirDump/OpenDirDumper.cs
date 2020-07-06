using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OpenDirDump
{
    public class OpenDirDumper
    {
        private Uri uri;
        private Random r = new Random();

        /* proxylist */
        List<Proxy> proxies = new List<Proxy>();
        public List<Proxy> Proxies { get { return this.proxies; } set { this.proxies = value; } }

        /* found links */
        private List<string> list = new List<string>();

        /* callbacks */
        private Action<Target> on_linkFound = null;
        public Action<Target> OnLinkFound { get { return this.on_linkFound; } set { this.on_linkFound = value; } }

        /* regular expressions */
        private Regex regex_url = new Regex(@"(https?:\/\/[-\w;\/?:@&=+$\|\\_.!~*\|'()\[\]%#,☺]+[\w\/#](\(\))?)(?=|[\s',\|\(\).:;?\-\[\]>\)])\=?", RegexOptions.IgnoreCase);
        private Regex regex_prop = new Regex("(src=|href=)\"(.*?)\"", RegexOptions.IgnoreCase);

        /* user-agent strings */
        private string[] useragents = new string[] {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.116 Safari/537.36",
            "Mozilla/5.0 (iPhone; CPU OS 11_0 like Mac OS X) AppleWebKit/604.1.25 (KHTML, like Gecko) Version/11.0 Mobile/15A372 Safari/604.1",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.106 Safari/537.36 Edg/83.0.478.54",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_13_6) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/13.1.1 Safari/605.1.15",
            "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:76.0) Gecko/20100101 Firefox/76.0",
            "Mozilla/5.0 (Windows NT 6.3; Win64; x64; rv:77.0) Gecko/20100101 Firefox/77.0",
        };

        public OpenDirDumper()
        {

        }

        public OpenDirDumper(Action<Target> on_linkFound)
        {
            this.on_linkFound = on_linkFound;
        }

        public void Dump(string url)
        {
            uri = new Uri(url);
            Task.Run(() => { Worker(this.uri); });
        }

        private Proxy GetRandomProxy()
        {
            return this.proxies.Count > 0 ? 
                this.proxies[this.r.Next(0, this.proxies.Count - 1)] : null;
        }

        private string GetRandomUserAgent()
        {
            return this.useragents.Length > 0 ?
                this.useragents[this.r.Next(0, this.useragents.Length - 1)] : "";
        }

        public bool Download(string remote, string local, int maxtries = 10)
        {
            int tries = 1;

            while (tries <= maxtries)
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        if (this.proxies.Count > 0)
                        {
                            client.Proxy = new WebProxy(GetRandomProxy().ToString());
                        }
                        client.Headers.Add("User-Agent", GetRandomUserAgent());
                        client.DownloadFile(remote, local);
                    }
                    return true;
                }
                catch (Exception)
                {
                }

                tries++;
            }
            return false;
        }

        public string RemoveQueryString(string url)
        {
            int i = url.IndexOf('?');
            if (i > -1)
            {
                return url.Remove(i);
            }
            return url;
        }

        public string RemoveDoubleSlashes(string url)
        {
            return url.Replace("//", "/");
        }

        public string RemoveFileName(string url)
        {
            int i = url.LastIndexOf('/');
            if (i > -1)
            {
                return url.Remove(i);
            }
            return url;
        }

        private long GetTime()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        private void Worker(Uri url)
        {
            int tries = 1; int maxtries = 10;

            while (tries <= maxtries)
            {
                try
                {
                    WebResponse response;
                    long time = GetTime();
                    string[] links = ExtractLinksFromURL(url.ToString(), out response);
                    long responseTime = GetTime() - time;

                    for (int i = 0; i < links.Length; i++)
                    {
                        string link = links[i];

                        if (link.StartsWith("/"))
                        {
                            link = url.Scheme + "://" + url.Host + link;
                        }
                        else if (!(link.StartsWith("http://") || link.StartsWith("https://")))
                        {
                            link = RemoveQueryString(url.ToString()).TrimEnd('/') + RemoveDoubleSlashes("/" + link);
                        }

                        Uri uri = new Uri(link);

                        if (!list.Contains(uri.ToString()))
                        {
                            list.Add(uri.ToString());

                            Target target = new Target
                            {
                                Url = uri,
                                Response = response,
                                BaseUrl = this.uri,
                                Scrape = false,
                                Tries = tries,
                                ResponseTime = responseTime,
                            };

                            on_linkFound?.Invoke(target);

                            if (target.Scrape)
                            {
                                Task.Run(() => { Worker(uri); });
                            }
                        }
                    }

                    break;
                }
                catch (Exception)
                {
                }

                tries++;
            }
        }

        private string GetFileExtension(string url)
        {
            string[] parts = url.Split('/');

            if (parts.Length > 0)
            {
                string last = parts[parts.Length - 1];
                if (last.Contains("."))
                {
                    string[] _parts = last.Split('.');
                    if (_parts[1].Length > 0)
                    {
                        return _parts[1].ToLower();
                    }
                }
            }

            return null;
        }

        private string[] ExtractLinksFromText(string text)
        {
            List<string> list = new List<string>();

            MatchCollection urls = regex_url.Matches(text);
            if (urls.Count > 0)
            {
                for (int i = 0; i < urls.Count; i++)
                {
                    list.Add(urls[i].Value.Trim());
                }
            }

            MatchCollection props = regex_prop.Matches(text);
            if (props.Count > 0)
            {
                for (int i = 0; i < props.Count; i++)
                {
                    string prop = props[i].Groups[2].Value;
                    if (!prop.Contains(" "))
                    {
                        list.Add(prop);
                    }
                }
            }

            return list.ToArray();
        }

        private string[] ExtractLinksFromURL(string url, out WebResponse response)
        {
            //using (WebClient client = new WebClient())
            //{
            //    if (this.proxies.Count > 0)
            //    {
            //        client.Proxy = new WebProxy(GetRandomProxy().ToString());
            //    }

            //    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.116 Safari/537.36");
            //    string text = client.DownloadString(url);

            //    return ExtractLinksFromText(text);
            //}

            WebRequest request = WebRequest.Create(url);
            request.Headers.Add("User-Agent", GetRandomUserAgent());
            if (this.proxies.Count > 0)
            {
                request.Proxy = new WebProxy(GetRandomProxy().ToString());
            }
            response = request.GetResponse();
            string text = new StreamReader(response.GetResponseStream()).ReadToEnd();

            List<string> list = new List<string>();
            list.Add(response.ResponseUri.ToString());
            list.AddRange(ExtractLinksFromText(text));
            return list.ToArray();
        }

        public bool LoadProxiesFromText(string input)
        {
            string[] lines = input.Split('\n');
            foreach (string line in lines)
            {
                if (line.Length == 0) continue;

                string[] pieces = line.Split(' ', '\t', ':', '<', '>');
                int mode = 0; Proxy proxy = new Proxy();
                foreach (string piece in pieces)
                {
                    if (mode == 0)
                    {
                        IPAddress ip = null;
                        bool valid = IPAddress.TryParse(piece, out ip);
                        if (valid)
                        {
                            proxy.Host = piece.ToLower();
                            mode = 1;
                        }
                    }
                    else
                    {
                        UInt16 portnum;
                        bool valid = UInt16.TryParse(piece, out portnum);
                        if (valid)
                        {
                            proxy.Port = portnum;
                            this.proxies.Add(proxy);
                            break;
                        }
                    }
                }
            }

            return this.proxies.Count > 0;
        }

        public bool LoadProxiesFromUrl(string url)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", GetRandomUserAgent());
                    string body = client.DownloadString(url);
                    return LoadProxiesFromText(body);
                }
            }
            catch (Exception)
            {
            }

            return false;
        }
    }

    public class Proxy
    {
        public string Protocol = "http";
        public string Host = "127.0.0.1";
        public UInt16 Port = 8080;

        public string CountryName = "";
        public string CountryCode = "";
        public long Ping = -1;

        public Proxy()
        {
        }

        public Proxy(string host, UInt16 port)
        {
            this.Host = host;
            this.Port = port;
        }

        public override string ToString()
        {
            return this.Protocol.ToLower() + "://" + this.Host + ":" + this.Port.ToString() + "/";
        }
    }

    public class Target
    {
        public Uri Url = null;
        public WebResponse Response = null;
        public Uri BaseUrl = null;
        public bool Scrape = false;
        public int Tries = 0;
        public long ResponseTime = 0;

        public bool Download(string localPath)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.116 Safari/537.36");
                    client.DownloadFile(this.Url.ToString(), localPath);
                }
                return true;
            }
            catch (Exception)
            {
            }
            return false;
        }

        public string GetExtension()
        {
            if (this.Url == null) return null;

            string[] parts = this.Url.Segments;
            if (parts[parts.Length - 1].Contains('.'))
            {
                string[] ext = parts[parts.Length - 1].Split('.');
                return ext[ext.Length - 1].ToLower();
            }

            return null;
        }
    }
}