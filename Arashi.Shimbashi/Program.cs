using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ARSoft.Tools.Net;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Arashi.AoiConfig;
using Timer = System.Timers.Timer;

namespace Arashi.Shimbashi
{
    class Program
    {
        static void Main(string[] args)
        {
            var cmd = new CommandLineApplication
            {
                Name = "Arashi.Shimbashi",
                Description = "ArashiDNS.Shimbashi - Lightweight DNS over HTTPS Server" +
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
            var buOption = cmd.Option<string>("-b|--backupstream <IPAddress>", "Set backup upstream DNS server IP address <8.8.8.8>",
                CommandOptionType.SingleValue);
            var timeoutOption = cmd.Option<int>("-t|--timeout <Timeout(ms)>", "Set timeout for query to upstream DNS server <500>",
                CommandOptionType.SingleValue);
            var retriesOption = cmd.Option<int>("-r|--retries <Int>", "Set number of retries for query to upstream DNS server <5>",
                CommandOptionType.SingleValue);
            var perfixOption = cmd.Option<string>("-p|--perfix <PerfixString>", "Set your DNS over HTTPS server query prefix </dns-query>",
                CommandOptionType.SingleValue);

            var cacheOption = cmd.Option("-c|--cache:<Type>", "Local query cache settings [full/flexible/none]", CommandOptionType.SingleOrNoValue);
            var logOption = cmd.Option("--log:<Type>", "Console log output settings [full/dns/none]", CommandOptionType.SingleOrNoValue);
            var chinaListOption = cmd.Option("-cn|--chinalist", "Set enable ChinaList", CommandOptionType.NoValue);
            var tcpOption = cmd.Option("--tcp", "Set enable upstream DNS query using TCP only", CommandOptionType.NoValue);
            var httpsOption = cmd.Option("-s|--https", "Set enable HTTPS", CommandOptionType.NoValue);
            var pfxOption = cmd.Option<string>("-pfx|--pfxfile <FilePath>", "Set your pfx certificate file path <./cert.pfx>",
                CommandOptionType.SingleOrNoValue);
            var pfxPassOption = cmd.Option<string>("-pass|--pfxpass <Password>", "Set your pfx certificate password <password>",
                CommandOptionType.SingleOrNoValue);
            var pemOption = cmd.Option<string>("-pem|--pemfile <FilePath>", "Set your pem certificate file path <./cert.pem>",
                CommandOptionType.SingleOrNoValue);
            var keyOption = cmd.Option<string>("-key|--keyfile <FilePath>", "Set your pem certificate key file path <./cert.key>",
                CommandOptionType.SingleOrNoValue);
            var syncmmdbOption = cmd.Option<string>("--syncmmdb", "Sync MaxMind GeoLite2 DB", CommandOptionType.NoValue);
            var synccnlsOption = cmd.Option<string>("--synccnls", "Sync China White List", CommandOptionType.NoValue);
            var noecsOption = cmd.Option("--noecs", "Set force disable active EDNS Client Subnet", CommandOptionType.NoValue);
            var showOption = cmd.Option("--show", "Show current active configuration", CommandOptionType.NoValue);
            var saveOption = cmd.Option("--save", "Save active configuration to config.json file", CommandOptionType.NoValue);
            var loadOption = cmd.Option<string>("--load:<FilePath>", "Load existing configuration from config.json file [./config.json]",
                CommandOptionType.SingleOrNoValue);
            var testOption = cmd.Option("-e|--test", "Exit after passing the test", CommandOptionType.NoValue);

            var ipipOption = cmd.Option("--ipip", string.Empty, CommandOptionType.NoValue);
            var rankOption = cmd.Option("--rank", string.Empty, CommandOptionType.NoValue);
            var adminOption = cmd.Option("--admin", string.Empty, CommandOptionType.NoValue);
            var noUpdateOption = cmd.Option("-nu|--noupdate", string.Empty, CommandOptionType.NoValue);
            ipipOption.ShowInHelpText = false;
            adminOption.ShowInHelpText = false;
            chinaListOption.ShowInHelpText = false;
            synccnlsOption.ShowInHelpText = false;
            noUpdateOption.ShowInHelpText = false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                pemOption.ShowInHelpText = false;
                keyOption.ShowInHelpText = false;
            }

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

                if ((File.Exists("/.dockerenv") ||
                     Environment.GetEnvironmentVariables().Contains("ARASHI_RUNNING_IN_CONTAINER") ||
                     Environment.GetEnvironmentVariables().Contains("ARASHI_ANY")) &&
                    !ipOption.HasValue()) ipEndPoint.Address = IPAddress.Any;

                if (Environment.GetEnvironmentVariables().Contains("PORT") && !ipOption.HasValue())
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

                if (Dns.GetHostAddresses(Dns.GetHostName()).All(ip =>
                    IPAddress.IsLoopback(ip) || ip.AddressFamily == AddressFamily.InterNetworkV6))
                {
                    Config.UpStream = "2001:4860:4860::8888";
                    Config.BackUpStream = "2001:4860:4860::8844";
                    Console.WriteLine("May run on IPv6 single stack network");
                }

                if (PortIsUse(53) && !upOption.HasValue())
                {
                    Config.UpStream = IPAddress.Loopback.ToString();
                    Console.WriteLine("Use localhost:53 dns server as upstream");
                }

                if (upOption.HasValue()) Config.UpStream = upOption.Value();
                if (buOption.HasValue()) Config.BackUpStream = buOption.Value();
                if (timeoutOption.HasValue()) Config.TimeOut = timeoutOption.ParsedValue;
                if (retriesOption.HasValue()) Config.Retries = retriesOption.ParsedValue;
                if (perfixOption.HasValue()) Config.QueryPerfix = "/" + perfixOption.Value().Trim('/').Trim('\\');
                Config.CacheEnable = cacheOption.HasValue();
                Config.ChinaListEnable = chinaListOption.HasValue();
                Config.RankEnable = rankOption.HasValue();
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

                if (Config.CacheEnable && Config.GeoCacheEnable || syncmmdbOption.HasValue() || Config.RankEnable)
                {
                    var setupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
                    Console.WriteLine(
                        "This product includes GeoLite2 data created by MaxMind, available from https://www.maxmind.com");
                    if (syncmmdbOption.HasValue())
                    {
                        if (File.Exists(setupBasePath + "GeoLite2-ASN.mmdb"))
                            File.Delete(setupBasePath + "GeoLite2-ASN.mmdb");
                        if (File.Exists(setupBasePath + "GeoLite2-City.mmdb"))
                            File.Delete(setupBasePath + "GeoLite2-City.mmdb");
                    }

                    if (!noUpdateOption.HasValue())
                    {
                        var timer = new Timer(100) {Enabled = true, AutoReset = true};
                        timer.Elapsed += (_, _) =>
                        {
                            timer.Interval = 3600000 * 24;
                            GetFileUpdate("GeoLite2-ASN.mmdb", Config.MaxmindAsnDbUrl);
                            GetFileUpdate("GeoLite2-City.mmdb", Config.MaxmindCityDbUrl);
                        };
                    }
                }

                if (synccnlsOption.HasValue())
                {
                    if (File.Exists(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "China_WhiteList.List"))
                        File.Delete(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "China_WhiteList.List");
                    var timer = new Timer(100) {Enabled = true, AutoReset = true};
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
                else if (File.Exists(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "China_WhiteList.List"))
                    GetFileUpdate("China_WhiteList.List", "https://mili.one/china_whitelist.txt");

                if (Config.UseAdminRoute)
                    Console.WriteLine(
                        $"Access Get AdminToken : /dns-admin/set-token?t={Config.AdminToken}");

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
                            if (!pfxOption.HasValue() && !pemOption.HasValue()) listenOptions.UseHttps();
                            if (pfxOption.HasValue())
                                if (pfxPassOption.HasValue())
                                    listenOptions.UseHttps(pfxOption.Value(), pfxPassOption.Value());
                                else listenOptions.UseHttps(pfxOption.Value());
                            if (pemOption.HasValue() && keyOption.HasValue())
                                listenOptions.UseHttps(X509Certificate2.CreateFromPem(
                                    File.ReadAllText(pemOption.Value()), File.ReadAllText(keyOption.Value())));
                        });
                    })
                    .UseStartup<Startup>()
                    .Build();
                if (testOption.HasValue())
                    Task.Run(() =>
                    {
                        for (int i = 0; i < 100; i++)
                            if (PortIsUse(ipEndPoint.Port))
                                host.StopAsync().Wait(5000);
                        Environment.Exit(0);
                    });
                if (saveOption.HasValue())
                    File.WriteAllText("config.json", JsonConvert.SerializeObject(Config, Formatting.Indented));
                if (showOption.HasValue()) Console.WriteLine(JsonConvert.SerializeObject(Config, Formatting.Indented));
                host.Run();
            });

            try
            {
                if (File.Exists("/.dockerenv") ||
                    Environment.GetEnvironmentVariables().Contains("DOTNET_RUNNING_IN_CONTAINER") ||
                    Environment.GetEnvironmentVariables().Contains("ARASHI_RUNNING_IN_CONTAINER"))
                    Console.WriteLine("ArashiDNS Running in Docker Container");
                if (Environment.GetEnvironmentVariables().Contains("ARASHI_VAR"))
                {
                    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ARASHI_VAR")))
                        throw new Exception();
                    cmd.Execute(Environment.GetEnvironmentVariable("ARASHI_VAR").Split(' '));
                }
                else
                    cmd.Execute(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                cmd.Execute();
            }
        }

        public static bool PortIsUse(int port)
        {
            var ipEndPointsTcp = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            var ipEndPointsUdp = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();

            return ipEndPointsTcp.Any(endPoint => endPoint.Port == port)
                   || ipEndPointsUdp.Any(endPoint => endPoint.Port == port);
        }

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
