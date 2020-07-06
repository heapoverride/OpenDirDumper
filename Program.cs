using System;
using System.Linq;
using System.IO;
using OpenDirDump;

class Program
{
    static void Main(string[] args)
    {
        OpenDirDumper dumper = new OpenDirDumper(target => {
            if (target.BaseUrl.Host != target.Url.Host) return;

            target.Scrape = target.Response.ContentType.Contains("text/html");
            Console.WriteLine(target.Url.ToString());
        });

        dumper.Dump("https://index-of.es/");

        Console.ReadLine();
    }
}