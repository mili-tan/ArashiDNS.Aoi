using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
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
            var adminOption = cmd.Option("--admin", string.Empty, CommandOptionType.NoValue);
            ipipOption.ShowInHelpText = false;
            adminOption.ShowInHelpText = false;
            chinaListOption.ShowInHelpText = false;
            synccnlsOption.ShowInHelpText = false;

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

                if (PortIsUse(53)) Config.UpStream = IPAddress.Loopback.ToString();
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
                    Console.WriteLine(
                        "This product includes GeoLite2 data created by MaxMind, available from https://www.maxmind.com");
                    if (syncmmdbOption.HasValue())
                    {
                        if (File.Exists(setupBasePath + "GeoLite2-ASN.mmdb")) File.Delete(setupBasePath + "GeoLite2-ASN.mmdb");
                        if (File.Exists(setupBasePath + "GeoLite2-City.mmdb")) File.Delete(setupBasePath + "GeoLite2-City.mmdb");
                    }

                    Console.Write("GeoLite2-ASN.mmdb Last updated: " +
                                      new FileInfo(setupBasePath + "GeoLite2-ASN.mmdb").LastWriteTimeUtc);
                    if (File.Exists(setupBasePath + "GeoLite2-ASN.mmdb") &&
                        (DateTime.UtcNow - new FileInfo(setupBasePath + "GeoLite2-ASN.mmdb").LastWriteTimeUtc)
                        .TotalDays > 7)
                    {
                        Console.WriteLine(
                            $" : Expired {(DateTime.UtcNow - new FileInfo(setupBasePath + "GeoLite2-ASN.mmdb").LastWriteTimeUtc).TotalDays:0} days");
                        File.Delete(setupBasePath + "GeoLite2-ASN.mmdb");
                    }
                    else Console.WriteLine();

                    Console.Write("GeoLite2-City.mmdb Last updated: " +
                                      new FileInfo(setupBasePath + "GeoLite2-City.mmdb").LastWriteTimeUtc);
                    if (File.Exists(setupBasePath + "GeoLite2-City.mmdb") &&
                        (DateTime.UtcNow - new FileInfo(setupBasePath + "GeoLite2-City.mmdb").LastWriteTimeUtc)
                        .TotalDays > 7)
                    {
                        Console.WriteLine(
                            $" : Expired {(DateTime.UtcNow - new FileInfo(setupBasePath + "GeoLite2-City.mmdb").LastWriteTimeUtc).TotalDays:0} days");
                        File.Delete(setupBasePath + "GeoLite2-City.mmdb");
                    }
                    else Console.WriteLine();

                    if (!File.Exists(setupBasePath + "GeoLite2-ASN.mmdb"))
                        Task.Run(() =>
                        {
                            Console.WriteLine("Downloading GeoLite2-ASN.mmdb...");
                            new WebClient().DownloadFile(Config.MaxmindAsnDbUrl, setupBasePath + "GeoLite2-ASN.mmdb");
                            Console.WriteLine("GeoLite2-ASN.mmdb Download Done");
                        });
                    if (!File.Exists(setupBasePath + "GeoLite2-City.mmdb"))
                        Task.Run(() =>
                        {
                            Console.WriteLine("Downloading GeoLite2-City.mmdb...");
                            new WebClient().DownloadFile(Config.MaxmindCityDbUrl, setupBasePath + "GeoLite2-City.mmdb");
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
            IPEndPoint[] ipEndPointsTcp = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            IPEndPoint[] ipEndPointsUdp = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();

            return ipEndPointsTcp.Any(endPoint => endPoint.Port == port)
                   || ipEndPointsUdp.Any(endPoint => endPoint.Port == port);
        }
    }
}
