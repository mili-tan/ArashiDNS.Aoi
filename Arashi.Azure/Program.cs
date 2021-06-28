using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ARSoft.Tools.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using static Arashi.AoiConfig;
using Timer = System.Timers.Timer;

namespace Arashi.Azure
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Config = new AoiConfig();
            Config.RankEnable = true;
            Config.ChinaListEnable = true;

            if (Config.CacheEnable && Config.GeoCacheEnable || Config.RankEnable)
            {
                Console.WriteLine(
                    "This product includes GeoLite2 data created by MaxMind, available from https://www.maxmind.com");
                var timer = new Timer(100) {Enabled = true, AutoReset = true};
                timer.Elapsed += (_, _) =>
                {
                    timer.Interval = 3600000 * 24;
                    GetFileUpdate("GeoLite2-ASN.mmdb", Config.MaxmindAsnDbUrl);
                    GetFileUpdate("GeoLite2-City.mmdb", Config.MaxmindCityDbUrl);
                };
            }

            if (Config.ChinaListEnable)
            {
                var timer = new Timer(100) { Enabled = true, AutoReset = true };
                timer.Elapsed += (_, _) =>
                {
                    timer.Interval = 3600000 * 24;
                    GetFileUpdate("China_WhiteList.List", "https://mili.one/china_whitelist.txt");
                    Task.Run(() =>
                    {
                        while (true)
                        {
                            if (File.Exists(DNSChinaConfig.Config.ChinaListPath))
                            {
                                File.ReadAllLines(DNSChinaConfig.Config.ChinaListPath).ToList()
                                    .ConvertAll(DomainName.Parse);
                                break;
                            }

                            Thread.Sleep(1000);
                        }
                    });
                };
            }

            File.WriteAllText("Token.txt", Config.AdminToken);
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(
                webBuilder => webBuilder.UseStartup<Startup>());

        public static void GetFileUpdate(string file, string url)
        {
            var setupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            if (File.Exists(setupBasePath + file))
                Console.Write(file + " Last updated: " + new FileInfo(setupBasePath + file).LastWriteTimeUtc);
            else Console.Write(file + " Not Exist or being Updating");
            if (File.Exists(setupBasePath + file) &&
                (DateTime.UtcNow - new FileInfo(setupBasePath + file).LastWriteTimeUtc)
                .TotalDays > 7)
            {
                Console.WriteLine(
                    $" : Expired {(DateTime.UtcNow - new FileInfo(setupBasePath + file).LastWriteTimeUtc).TotalDays:0} days");
                File.Delete(setupBasePath + file);
            }
            else Console.WriteLine();

            if (!File.Exists(setupBasePath + file))
                Task.Run(() =>
                {
                    Console.WriteLine($"Downloading {file}...");
                    new WebClient().DownloadFile(url, setupBasePath + file);
                    Console.WriteLine(file + " Download Done");
                });
        }
    }
}
