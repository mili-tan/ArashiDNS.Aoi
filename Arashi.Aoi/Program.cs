using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Arashi.AoiConfig;
using Timer = System.Timers.Timer;

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
            var notlimitedOption = cmd.Option("--notlimited", "Set disable access rate limit, can be configured in appsettings.json", CommandOptionType.NoValue);
            var transidOption = cmd.Option("--transid", "Set enable DNS Transaction ID", CommandOptionType.NoValue);
            var showOption = cmd.Option("--show", "Show current active configuration", CommandOptionType.NoValue);
            var saveOption = cmd.Option("--save", "Save active configuration to config.json file", CommandOptionType.NoValue);
            var loadOption = cmd.Option<string>("--load:<FilePath>", "Load existing configuration from config.json file [./config.json]",
                CommandOptionType.SingleOrNoValue);
            var loadcnOption = cmd.Option<string>("--loadcn:<FilePath>", "Load existing configuration from cnlist.json file [./cnlist.json]",
                CommandOptionType.SingleOrNoValue);
            var testOption = cmd.Option("-e|--test", "Exit after passing the test", CommandOptionType.NoValue);

            var ipipOption = cmd.Option("--ipip", string.Empty, CommandOptionType.NoValue);
            var udsOption = cmd.Option("--uds", string.Empty, CommandOptionType.NoValue);
            //var rankOption = cmd.Option("--rank", string.Empty, CommandOptionType.NoValue);
            var adminOption = cmd.Option("--admin", string.Empty, CommandOptionType.NoValue);
            var noUpdateOption = cmd.Option("-nu|--noupdate", string.Empty, CommandOptionType.NoValue);
            var anyOption = cmd.Option("--any", string.Empty, CommandOptionType.NoValue);
            ipipOption.ShowInHelpText = false;
            adminOption.ShowInHelpText = false;
            synccnlsOption.ShowInHelpText = false;
            noUpdateOption.ShowInHelpText = false;
            chinaListOption.ShowInHelpText = false;
            loadcnOption.ShowInHelpText = false;
            anyOption.ShowInHelpText = false;

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
                if (loadcnOption.HasValue())
                    DNSChinaConfig.Config = JsonConvert.DeserializeObject<DNSChinaConfig>(
                        string.IsNullOrWhiteSpace(loadcnOption.Value())
                            ? File.ReadAllText("cnlist.json")
                            : File.ReadAllText(loadcnOption.Value()));

                Console.WriteLine(cmd.Description);
                var ipEndPoint = ipOption.HasValue()
                    ? IPEndPoint.Parse(ipOption.Value())
                    : httpsOption.HasValue()
                        ? new IPEndPoint(IPAddress.Loopback, 443)
                        : new IPEndPoint(IPAddress.Loopback, 2020);

                if ((Environment.GetEnvironmentVariables().Contains("ARASHI_RUNNING_IN_CONTAINER") ||
                     Environment.GetEnvironmentVariables().Contains("ARASHI_ANY") || File.Exists("/.dockerenv")) &&
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

                try
                {
                    if (Dns.GetHostAddresses(Dns.GetHostName()).All(ip =>
                            IPAddress.IsLoopback(ip) || ip.AddressFamily == AddressFamily.InterNetworkV6))
                    {
                        Config.UpStream = "2001:4860:4860::8888";
                        Config.BackUpStream = "2001:4860:4860::8844";
                        Console.WriteLine("May run on IPv6 single stack network");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                try
                {
                    if (PortIsUse(53) && LocalDnsIsOk() && !upOption.HasValue())
                    {
                        Config.UpStream = IPAddress.Loopback.ToString();
                        Console.WriteLine("Use localhost:53 dns server as upstream");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                if (upOption.HasValue()) Config.UpStream = upOption.Value();
                if (buOption.HasValue()) Config.BackUpStream = buOption.Value();
                if (timeoutOption.HasValue()) Config.TimeOut = timeoutOption.ParsedValue;
                if (retriesOption.HasValue()) Config.Retries = retriesOption.ParsedValue;
                if (perfixOption.HasValue()) Config.QueryPerfix = "/" + perfixOption.Value().Trim('/').Trim('\\');
                Config.CacheEnable = cacheOption.HasValue();
                Config.ChinaListEnable = chinaListOption.HasValue();
                //Config.RankEnable = rankOption.HasValue();
                Config.LogEnable = logOption.HasValue();
                Config.OnlyTcpEnable = tcpOption.HasValue();
                Config.EcsEnable = !noecsOption.HasValue();
                Config.RateLimitingEnable = !notlimitedOption.HasValue();
                Config.UseAdminRoute = adminOption.HasValue();
                Config.UseIpRoute = ipipOption.HasValue();
                Config.TransIdEnable = transidOption.HasValue();
                Config.AnyTypeEnable = anyOption.HasValue();
                if (logOption.HasValue() && !string.IsNullOrWhiteSpace(logOption.Value()))
                {
                    var val = logOption.Value().ToLower().Trim();
                    if (val == "full") Config.FullLogEnable = true;
                    else if (val is "none" or "null" or "off") Config.LogEnable = false;
                }

                if (cacheOption.HasValue() && !string.IsNullOrWhiteSpace(cacheOption.Value()))
                {
                    var val = cacheOption.Value().ToLower().Trim();
                    if (val == "full") Config.GeoCacheEnable = false;
                    else if (val is "none" or "null" or "off") Config.CacheEnable = false;
                }

                if (Config.CacheEnable && Config.GeoCacheEnable || syncmmdbOption.HasValue() /*|| Config.RankEnable*/)
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
                            Parallel.Invoke(
                                () => GetFileUpdate("GeoLite2-ASN.mmdb", Config.MaxmindAsnDbUrl),
                                () => GetFileUpdate("GeoLite2-City.mmdb", Config.MaxmindCityDbUrl));
                        };
                    }
                }

                if (synccnlsOption.HasValue())
                {
                    var cnListPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "China_WhiteList.List";
                    if (File.Exists(cnListPath))
                        File.Delete(cnListPath);
                    var timer = new Timer(100) {Enabled = true, AutoReset = true};
                    timer.Elapsed += (_, _) =>
                    {
                        timer.Interval = 3600000 * 24;
                        GetFileUpdate("China_WhiteList.List", DNSChinaConfig.Config.ChinaListUrl);
                        //Task.Run(() =>
                        //{
                        //    while (true)
                        //    {
                        //        if (File.Exists(cnListPath))
                        //        {
                        //            DNSChina.ChinaList = File.ReadAllLines(cnListPath).ToList()
                        //                .ConvertAll(DomainName.Parse);
                        //            break;
                        //        }

                        //        Thread.Sleep(1000);
                        //    }
                        //});
                    };
                }
                else if (File.Exists(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "China_WhiteList.List"))
                    GetFileUpdate("China_WhiteList.List", DNSChinaConfig.Config.ChinaListUrl);

                if (Config.UseAdminRoute)
                    Console.WriteLine(
                        $"Access Get AdminToken : /dns-admin/set-token?t={Config.AdminToken}");

                if (!OperatingSystem.IsWindows() && File.Exists("/tmp/arashi-a.sock") && udsOption.HasValue())
                {
                    try
                    {
                        File.Delete("/tmp/arashi-a.sock");
                        File.Create("/tmp/arashi-a.sock").Close();
                        ChMod.Set("/tmp/arashi-a.sock");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
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
                        options.Limits.MaxResponseBufferSize = 2048;
                        options.Listen(ipEndPoint, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
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
                        if (!OperatingSystem.IsWindows() && udsOption.HasValue()) options.ListenUnixSocket("/tmp/arashi-a.sock");
                    })
                    .UseStartup<Startup>()
                    .Build();

                if (testOption.HasValue())
                    Task.Run(() =>
                    {
                        for (var i = 0; i < 100; i++)
                            if (PortIsUse(ipEndPoint.Port))
                                host.StopAsync().Wait(5000);
                        Environment.Exit(0);
                    });
                if (saveOption.HasValue())
                {
                    File.WriteAllText("config.json", JsonConvert.SerializeObject(Config, Formatting.Indented));
                    if (Config.ChinaListEnable)
                        File.WriteAllText("cnlist.json",
                            JsonConvert.SerializeObject(DNSChinaConfig.Config, Formatting.Indented));
                    Console.WriteLine("Configuration Saved.");
                }

                if (showOption.HasValue()) Console.WriteLine(JsonConvert.SerializeObject(Config, Formatting.Indented));
                host.Run();
            });

            try
            {
                if (Environment.GetEnvironmentVariables().Contains("DOTNET_RUNNING_IN_CONTAINER") ||
                    Environment.GetEnvironmentVariables().Contains("ARASHI_RUNNING_IN_CONTAINER") ||
                    File.Exists("/.dockerenv"))
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

        public static bool LocalDnsIsOk()
        {
            try
            {
                return new DnsClient(IPAddress.Loopback, 500)
                           .Resolve(DomainName.Parse("example.com")).ReturnCode ==
                       ReturnCode.NoError;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
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

            if (File.Exists(setupBasePath + file)) return;
            Console.WriteLine($"Downloading {file}...");
            File.WriteAllBytes(setupBasePath + file, new HttpClient().GetByteArrayAsync(url).Result);
            Console.WriteLine(file + " Download Done");
        }
    }

    public static class ChMod
    {
        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "interop")]
        public static extern int Chmod(string pathname, int mode);

        // user permissions
        const int S_IRUSR = 0x100;
        const int S_IWUSR = 0x80;
        const int S_IXUSR = 0x40;

        // group permission
        const int S_IRGRP = 0x20;
        const int S_IWGRP = 0x10;
        const int S_IXGRP = 0x8;

        // other permissions
        const int S_IROTH = 0x4;
        const int S_IWOTH = 0x2;
        const int S_IXOTH = 0x1;

        public static void Set(string filename)
        {
            const int _0755 =
                S_IRUSR | S_IXUSR | S_IWUSR
                | S_IRGRP | S_IXGRP | S_IWGRP
                | S_IROTH | S_IXOTH | S_IWOTH;

            if (0 != Chmod(Path.GetFullPath(filename), (int)_0755))
                throw new Exception("Could not set Unix socket permissions");
        }
    }
}
