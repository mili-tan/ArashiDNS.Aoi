using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Arashi.Azure;
using LettuceEncrypt;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Arashi.Aoi
{
    class Program
    {
        static void Main(string[] args)
        {
            var cmd = new CommandLineApplication
                {Name = "Arashi.Aoi", Description = "ArashiDNS.Aoi - Simple Lightweight DNS over HTTPS Server"};

            cmd.HelpOption("-?|-h|--help");
            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>", "Set listen ip address and port <127.0.0.1:2020>",
                CommandOptionType.SingleValue);
            var upOption = cmd.Option<string>("-u|--upstream <IPAddress>", "Set upstream ip address <8.8.8.8>",
                CommandOptionType.SingleValue);
            var timeoutOption = cmd.Option<int>("-t|--timeout <Timeout(ms)>", "Set upstream query timeout <500>",
                CommandOptionType.SingleValue);
            var perfixOption = cmd.Option<string>("-p|--perfix <PerfixString>",
                "Set https query perfix </dns-query>",
                CommandOptionType.SingleValue);

            var cacheOption = cmd.Option("--cache:<Type>", "Set enable caching [full/flexible/none]", CommandOptionType.SingleOrNoValue);
            var logOption = cmd.Option("--log:<Type>", "Set enable log [full/dns/none]", CommandOptionType.SingleOrNoValue);
            var chinaListOption = cmd.Option("--chinalist", "Set enable ChinaList", CommandOptionType.NoValue);
            var tcpOption = cmd.Option("--tcp", "Set enable only TCP query", CommandOptionType.NoValue);
            var httpsOption = cmd.Option("-s|--https", "Set enable HTTPS", CommandOptionType.NoValue);
            var pfxOption = cmd.Option<string>("-pfx|--pfxfile <FilePath>", "Set pfx file path <./cert.pfx>[@<password>]",
                CommandOptionType.SingleValue);
            var letsencryptOption = cmd.Option<string>("-let|--letsencrypt <ApplyString>", "Apply LetsEncrypt <domain.name>:<you@your.email>",
                CommandOptionType.SingleValue);
            chinaListOption.ShowInHelpText = false;
            letsencryptOption.ShowInHelpText = false;

            cmd.OnExecute(() =>
            {
                Console.WriteLine(cmd.Description);
                var ipEndPoint = ipOption.HasValue()
                    ? IPEndPoint.Parse(ipOption.Value())
                    : httpsOption.HasValue()
                        ? new IPEndPoint(IPAddress.Loopback, 443)
                        : new IPEndPoint(IPAddress.Loopback, 2020);
                if (upOption.HasValue()) Config.UpStream = IPAddress.Parse(upOption.Value());
                if (timeoutOption.HasValue()) Config.TimeOut = timeoutOption.ParsedValue;
                if (perfixOption.HasValue()) Config.QueryPerfix = "/" + perfixOption.Value().Trim('/').Trim('\\');
                Config.CacheEnable = cacheOption.HasValue();
                Config.ChinaListEnable = chinaListOption.HasValue();
                Config.LogEnable = logOption.HasValue();
                Config.OnlyTcpEnable = tcpOption.HasValue();
                Config.UseIpRoute = false;
                Config.UseCacheRoute = false;
                if (logOption.HasValue() && !string.IsNullOrWhiteSpace(logOption.Value()))
                {
                    var val = logOption.Value().ToLower().Trim();
                    if (val == "full") Config.FullLogEnable = true;
                    if (val == "none" || val == "null" || val == "off") Config.LogEnable = false;
                }
                if (cacheOption.HasValue() && !string.IsNullOrWhiteSpace(cacheOption.Value()))
                {
                    var val = cacheOption.Value().ToLower().Trim();
                    if (val == "full") Config.GeoCacheEnable = false;
                    if (val == "none" || val == "null" || val == "off") Config.CacheEnable = false;
                }
                if (Config.CacheEnable && Config.GeoCacheEnable)
                {
                    if (!File.Exists("GeoLite2-ASN.mmdb"))
                        Task.Run(() =>
                        {
                            Console.WriteLine("Downloading GeoLite2-ASN.mmdb...");
                            new WebClient().DownloadFile(
                                "https://gh.mili.one/" +
                                "https:/github.com/mili-tan/maxmind-geoip/releases/latest/download/GeoLite2-ASN.mmdb",
                                "GeoLite2-ASN.mmdb");
                            Console.WriteLine("GeoLite2-ASN.mmdb Download Done");
                        });
                    if (!File.Exists("GeoLite2-City.mmdb"))
                        Task.Run(() =>
                        {
                            Console.WriteLine("Downloading GeoLite2-City.mmdb...");
                            new WebClient().DownloadFile(
                                "https://gh.mili.one/" +
                                "https:/github.com/mili-tan/maxmind-geoip/releases/latest/download/GeoLite2-City.mmdb",
                                "GeoLite2-City.mmdb");
                            Console.WriteLine("GeoLite2-City.mmdb Download Done");
                        });
                }

                var host = new WebHostBuilder()
                    .UseKestrel()
                    .UseContentRoot(AppDomain.CurrentDomain.SetupInformation.ApplicationBase)
                    .ConfigureLogging(configureLogging =>
                    {
                        if (Config.LogEnable && Config.FullLogEnable) configureLogging.AddConsole();
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        if (httpsOption.HasValue() && letsencryptOption.HasValue())
                            services.AddLettuceEncrypt(configure =>
                            {
                                var letStrings = letsencryptOption.Value().Split(':');
                                configure.AcceptTermsOfService = true;
                                configure.DomainNames = new[] {letStrings[0]};
                                configure.EmailAddress = letStrings[1];
                            }).PersistDataToDirectory(new DirectoryInfo("/LettuceEncrypt"), null);
                    })
                    .ConfigureKestrel(options =>
                    {
                        options.Limits.MaxRequestBodySize = 1024;
                        options.Listen(ipEndPoint, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                            if (!httpsOption.HasValue()) return;
                            if (!pfxOption.HasValue()) listenOptions.UseHttps();
                            else
                            {
                                var pfxStrings = pfxOption.Value().Split('@');
                                if (pfxStrings.Length > 1)
                                    listenOptions.UseHttps(pfxStrings[0], pfxStrings[1]);
                                else listenOptions.UseHttps(pfxOption.Value());
                            }
                        });
                    })
                    .UseStartup<Startup>()
                    .Build();

                host.Run();
            });

            cmd.Execute(args);
        }
    }
}
