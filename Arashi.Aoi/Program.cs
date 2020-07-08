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
            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>", "Set the server listening address and port <127.0.0.1:2020>",
                CommandOptionType.SingleValue);
            var upOption = cmd.Option<string>("-u|--upstream <IPAddress>", "Set the upstream origin DNS server IP address <8.8.8.8>",
                CommandOptionType.SingleValue);
            var timeoutOption = cmd.Option<int>("-t|--timeout <Timeout(ms)>", "Set timeout for query to the upstream DNS server <500>",
                CommandOptionType.SingleValue);
            var perfixOption = cmd.Option<string>("-p|--perfix <PerfixString>", "Set your DNS over HTTPS server query prefix </dns-query>",
                CommandOptionType.SingleValue);

            var cacheOption = cmd.Option("-c|--cache:<Type>", "Local query cache settings [full/flexible/none]", CommandOptionType.SingleOrNoValue);
            var logOption = cmd.Option("--log:<Type>", "Console log output settings [full/dns/none]", CommandOptionType.SingleOrNoValue);
            var chinaListOption = cmd.Option("--chinalist", "Set enable ChinaList", CommandOptionType.NoValue);
            var tcpOption = cmd.Option("--tcp", "Set enable upstream DNS query using TCP only", CommandOptionType.NoValue);
            var httpsOption = cmd.Option("-s|--https", "Set enable HTTPS", CommandOptionType.NoValue);
            var pfxOption = cmd.Option<string>("-pfx|--pfxfile <FilePath>", "Set your pfx certificate file path <./cert.pfx>[@<password>]",
                CommandOptionType.SingleValue);
            var letsencryptOption = cmd.Option<string>("-let|--letsencrypt <ApplyString>", "Apply LetsEncrypt <domain.name>:<you@your.email>",
                CommandOptionType.SingleValue);
            var syncmmdbOption = cmd.Option<string>("--syncmmdb", "Sync MaxMind GeoLite2 DB", CommandOptionType.NoValue);
            
            var ipipOption = cmd.Option("--ipip", string.Empty, CommandOptionType.NoValue);
            var lschOption = cmd.Option("--lsch", string.Empty, CommandOptionType.NoValue);
            ipipOption.ShowInHelpText = false;
            lschOption.ShowInHelpText = false;
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
                Config.UseCacheRoute = lschOption.HasValue();
                Config.UseIpRoute = ipipOption.HasValue();
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
                if ((Config.CacheEnable && Config.GeoCacheEnable) || syncmmdbOption.HasValue())
                {
                    var SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
                    Console.WriteLine("This product includes GeoLite2 data created by MaxMind, available from https://www.maxmind.com");
                    if (syncmmdbOption.HasValue())
                    {
                        if (File.Exists(SetupBasePath+"GeoLite2-ASN.mmdb")) File.Delete(SetupBasePath + "GeoLite2-ASN.mmdb");
                        if (File.Exists(SetupBasePath + "GeoLite2-City.mmdb")) File.Delete(SetupBasePath + "GeoLite2-City.mmdb");
                    }
                    if (!File.Exists(SetupBasePath + "GeoLite2-ASN.mmdb"))
                        Task.Run(() =>
                        {
                            Console.WriteLine("Downloading GeoLite2-ASN.mmdb...");
                            new WebClient().DownloadFile(
                                "https://gh.mili.one/" +
                                "https:/github.com/mili-tan/maxmind-geoip/releases/latest/download/GeoLite2-ASN.mmdb",
                                SetupBasePath + "GeoLite2-ASN.mmdb");
                            Console.WriteLine("GeoLite2-ASN.mmdb Download Done");
                        });
                    if (!File.Exists(SetupBasePath + "GeoLite2-City.mmdb"))
                        Task.Run(() =>
                        {
                            Console.WriteLine("Downloading GeoLite2-City.mmdb...");
                            new WebClient().DownloadFile(
                                "https://gh.mili.one/" +
                                "https:/github.com/mili-tan/maxmind-geoip/releases/latest/download/GeoLite2-City.mmdb",
                                SetupBasePath + "GeoLite2-City.mmdb");
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
