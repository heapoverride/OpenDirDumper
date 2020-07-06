using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace OpenDirDump
{
    class ProxyChecker
    {
        private string checkurl = "http://router.unrealsec.eu/azenv/";
        private string checkstr = "Hello world!";
        private Random r = new Random();
        private List<Proxy> proxies = new List<Proxy>();
        public List<Proxy> Proxies { get { return this.proxies; } set { this.proxies = value; } }

        private Action<Proxy> aliveProxyFound = null;
        public Action<Proxy> AliveProxyFound { get { return this.aliveProxyFound; } set { this.aliveProxyFound = value; } }

        public ProxyChecker()
        {

        }

        public ProxyChecker(Action<Proxy> aliveProxyFound)
        {
            this.aliveProxyFound = aliveProxyFound;
        }

        public void Start()
        {
            for (int i = 0; i < this.proxies.Count; i++)
            {
                Task task = new Task(Worker);
                task.Start();
            }
        }

        private Proxy GetNextProxy()
        {
            // get random proxy and remove it 
            // from the list
            if (this.proxies.Count > 0)
            {
                int n = this.r.Next(0, this.proxies.Count - 1);
                Proxy proxy = this.proxies[n];
                lock (this.proxies)
                {
                    this.proxies.RemoveAt(n);
                }
                return proxy;
            }
            return null;
        }

        private long GetTime()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        public void Worker()
        {
            Proxy proxy = GetNextProxy();

            if (proxy != null)
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.116 Safari/537.36");
                        client.Proxy = new WebProxy(proxy.ToString());

                        long startTime = GetTime();
                        string body = client.DownloadString(this.checkurl);
                        long loadTime = GetTime();
                        proxy.ResponseTime = loadTime - startTime;

                        if (body.EndsWith(this.checkstr))
                        {
                            // valid and working proxy
                            var data = GetData(body);
                            if (data.ContainsKey("COUNTRY_NAME"))
                            {
                                proxy.CountryName = data["COUNTRY_NAME"];
                            }
                            if (data.ContainsKey("COUNTRY_CODE"))
                            {
                                proxy.CountryCode = data["COUNTRY_CODE"].ToUpper();
                            }

                            // callback
                            aliveProxyFound?.Invoke(proxy);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private Dictionary<string, string> GetData(string body)
        {
            string[] lines = body.Split('\n');
            Dictionary<string, string> dict = new Dictionary<string, string>();

            foreach (string line in lines)
            {
                if (line.Length == 0) continue;

                string[] pieces = line.Split('=');
                if (pieces.Length == 2)
                {
                    string key = pieces[0].Trim().ToUpper();
                    if (!dict.ContainsKey(key))
                    {
                        dict.Add(key, pieces[1].Trim());
                    }
                }
            }

            return dict;
        }

        public void Add(string host, UInt16 port)
        {
            this.proxies.Add(new Proxy(host, port));
        }

        public bool LoadProxiesFromUrl(string url)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.116 Safari/537.36");
                    string[] lines = client.DownloadString(url).Split('\n');
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
            }
            catch (Exception)
            {
            }

            return false;
        }

        public class Proxy
        {
            public string Protocol = "http";
            public string Host = "127.0.0.1";
            public UInt16 Port = 8080;

            public string CountryName = "";
            public string CountryCode = "";
            public long ResponseTime = 0;

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
    }
}