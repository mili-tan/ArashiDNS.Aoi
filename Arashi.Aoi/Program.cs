using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Arashi.Azure;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Arashi.AoiConfig;

namespace Arashi.Aoi
{
    class Program
    {
        static void Main(string[] args)
        {
            var cmd = new CommandLineApplication
            {
                Name = "Arashi.Aoi",
                Description = "ArashiDNS.Aoi - Lightweight DNS over HTTPS Server" +
                              Environment.NewLine +
                              $"Copyright (c) {DateTime.Now.Year} Milkey Tan. Code released under the Mozilla Public License 2.0" +
                              Environment.NewLine +
                              "https://github.com/mili-tan/ArashiDNS.Aoi/blob/master/CREDITS.md"
            };
            cmd.HelpOption("-?|-h|--help");
            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>", "Set server listening address and port <127.0.0.1:2020>",
                CommandOptionType.SingleValue);
            var upOption = cmd.Option<string>("-u|--upstream <IPAddress>", "Set upstream origin DNS server IP address <8.8.8.8>",
                CommandOptionType.SingleValue);
            var timeoutOption = cmd.Option<int>("-t|--timeout <Timeout(ms)>", "Set timeout for query to upstream DNS server <500>",
                CommandOptionType.SingleValue);
            var retriesOption = cmd.Option<int>("-r|--retries <Int>", "Set number of retries for query to upstream DNS server <5>",
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
            var syncmmdbOption = cmd.Option<string>("--syncmmdb", "Sync MaxMind GeoLite2 DB", CommandOptionType.NoValue);
            var synccnlsOption = cmd.Option<string>("--synccnls", "Sync China White List", CommandOptionType.NoValue);
            var noecsOption = cmd.Option("--noecs", "Set force disable active EDNS Client Subnet", CommandOptionType.NoValue);
            var showOption = cmd.Option("--show", "Show current active configuration", CommandOptionType.NoValue);
            var saveOption = cmd.Option("--save", "Save active configuration to config.json file", CommandOptionType.NoValue);
            var loadOption = cmd.Option<string>("--load:<FilePath>", "Load existing configuration from config.json file [./config.json]",
                CommandOptionType.SingleOrNoValue);

            var ipipOption = cmd.Option("--ipip", string.Empty, CommandOptionType.NoValue);
            var adminOption = cmd.Option("--admin", string.Empty, CommandOptionType.NoValue);
            ipipOption.ShowInHelpText = false;
            adminOption.ShowInHelpText = false;
            chinaListOption.ShowInHelpText = false;
            synccnlsOption.ShowInHelpText = false;

            cmd.OnExecute(() =>
            {
                if (loadOption.HasValue())
                    Config = JsonConvert.DeserializeObject<AoiConfig>(
                        string.IsNullOrWhiteSpace(loadOption.Value())
                            ? File.ReadAllText("config.json")
                            : File.ReadAllText(loadOption.Value()));
                Console.WriteLine(cmd.Description);
                var ipEndPoint = ipOption.HasValue()
                    ? IPEndPoint.Parse(ipOption.Value())
                    : httpsOption.HasValue()
                        ? new IPEndPoint(IPAddress.Loopback, 443)
                        : new IPEndPoint(IPAddress.Loopback, 2020);
                if (upOption.HasValue()) Config.UpStream = upOption.Value();
                if (timeoutOption.HasValue()) Config.TimeOut = timeoutOption.ParsedValue;
                if (retriesOption.HasValue()) Config.Retries = retriesOption.ParsedValue;
                if (perfixOption.HasValue()) Config.QueryPerfix = "/" + perfixOption.Value().Trim('/').Trim('\\');
                Config.CacheEnable = cacheOption.HasValue();
                Config.ChinaListEnable = chinaListOption.HasValue();
                Config.LogEnable = logOption.HasValue();
                Config.OnlyTcpEnable = tcpOption.HasValue();
                Config.EcsEnable = !noecsOption.HasValue();
                Config.UseAdminRoute = adminOption.HasValue();
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
                if (Config.CacheEnable && Config.GeoCacheEnable || syncmmdbOption.HasValue())
                {
                    var setupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
                    Console.WriteLine("This product includes GeoLite2 data created by MaxMind, available from https://www.maxmind.com");
                    if (syncmmdbOption.HasValue())
                    {
                        if (File.Exists(setupBasePath+"GeoLite2-ASN.mmdb")) File.Delete(setupBasePath + "GeoLite2-ASN.mmdb");
                        if (File.Exists(setupBasePath + "GeoLite2-City.mmdb")) File.Delete(setupBasePath + "GeoLite2-City.mmdb");
                    }
                    if (!File.Exists(setupBasePath + "GeoLite2-ASN.mmdb"))
                        Task.Run(() =>
                        {
                            Console.WriteLine("Downloading GeoLite2-ASN.mmdb...");
                            new WebClient().DownloadFile(
                                "https://gh.mili.one/" +
                                "github.com/mili-tan/maxmind-geoip/releases/latest/download/GeoLite2-ASN.mmdb",
                                setupBasePath + "GeoLite2-ASN.mmdb");
                            Console.WriteLine("GeoLite2-ASN.mmdb Download Done");
                        });
                    if (!File.Exists(setupBasePath + "GeoLite2-City.mmdb"))
                        Task.Run(() =>
                        {
                            Console.WriteLine("Downloading GeoLite2-City.mmdb...");
                            new WebClient().DownloadFile(
                                "https://gh.mili.one/" +
                                "github.com/mili-tan/maxmind-geoip/releases/latest/download/GeoLite2-City.mmdb",
                                setupBasePath + "GeoLite2-City.mmdb");
                            Console.WriteLine("GeoLite2-City.mmdb Download Done");
                        });
                }

                if (synccnlsOption.HasValue())
                {
                    Task.Run(() =>
                    {
                        Console.WriteLine("Downloading China_WhiteList.List...");
                        new WebClient().DownloadFile(
                            "https://mili.one/china_whitelist.txt",
                            AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "china_whitelist.list");
                        Console.WriteLine("China_WhiteList.List Download Done");
                    });
                }

                if (Config.UseAdminRoute) Console.WriteLine(
                    $"Access Get AdminToken : /dns-admin/set-token?t={Config.AdminToken}");

                if (File.Exists("/.dockerenv"))
                {
                    ipEndPoint.Address = IPAddress.Any;
                    try
                    {
                        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORT")))
                            throw new Exception();
                        ipEndPoint.Port = Convert.ToInt32(Environment.GetEnvironmentVariable("PORT"));
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Failed to get $PORT Environment Variable");
                    }
                }

                var host = new WebHostBuilder()
                    .UseKestrel()
                    .UseContentRoot(AppDomain.CurrentDomain.SetupInformation.ApplicationBase)
                    .ConfigureLogging(configureLogging =>
                    {
                        if (Config.LogEnable && Config.FullLogEnable) configureLogging.AddConsole();
                    })
                    .ConfigureServices(services => services.AddRouting())
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
                                    listenOptions.UseHttps(pfxStrings[0].Trim(), pfxStrings[1].Trim());
                                else listenOptions.UseHttps(pfxOption.Value());
                            }
                        });
                    })
                    .UseStartup<Startup>()
                    .Build();

                if (saveOption.HasValue())
                    File.WriteAllText("config.json", JsonConvert.SerializeObject(Config, Formatting.Indented));
                if (showOption.HasValue()) Console.WriteLine(JsonConvert.SerializeObject(Config, Formatting.Indented));
                host.Run();
            });

            if (File.Exists("/.dockerenv"))
            {
                Console.WriteLine("Running in Docker Container");
                try
                {
                    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VAR"))) 
                        throw new Exception();
                    cmd.Execute(Environment.GetEnvironmentVariable("VAR").Split(' '));
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to get $VAR Environment Variable");
                    cmd.Execute(args);
                }
            }
            else cmd.Execute(args);
        }
    }
}
