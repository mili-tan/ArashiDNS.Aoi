using System;
using System.IO;
using System.Linq;
using System.Net;
using Arashi.Azure;
using LettuceEncrypt;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

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
                "Set https query perfix <\"/dns-query\">",
                CommandOptionType.SingleValue);

            var cacheOption = cmd.Option("--cache:<Type>", "Set enable caching <full/flexible/none>", CommandOptionType.SingleOrNoValue);
            var chinaListOption = cmd.Option("--chinalist", "Set enable ChinaList", CommandOptionType.NoValue);
            var logOption = cmd.Option("--log", "Set enable log", CommandOptionType.NoValue);
            var tcpOption = cmd.Option("--tcp", "Set enable only TCP query", CommandOptionType.NoValue);
            var httpsOption = cmd.Option("--https", "Set enable HTTPS", CommandOptionType.NoValue);
            var pfxOption = cmd.Option<string>("--pfx <FilePath>", "Set pfx file path <./cert.pfx>",
                CommandOptionType.SingleValue);
            var pfxpassOption = cmd.Option<string>("--pfx-pass <Password>", "Set pfx file password <Password>",
                CommandOptionType.SingleValue);
            chinaListOption.ShowInHelpText = false;

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
                if (cacheOption.HasValue() && !string.IsNullOrWhiteSpace(cacheOption.Value()))
                {
                    var val = cacheOption.Value().ToLower().Trim();
                    if (val == "full")
                        Config.GeoCacheEnable = false;
                    if (val == "none" || val == "null" || val == "off")
                        Config.CacheEnable = false;
                }

                var host = new WebHostBuilder()
                    .UseKestrel()
                    .UseContentRoot(AppDomain.CurrentDomain.SetupInformation.ApplicationBase)
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddLettuceEncrypt(configure =>
                        {
                            configure.AcceptTermsOfService = true;
                            configure.EmailAddress = "me@mili.one";
                            configure.DomainNames = new[] {"me.mili.one"};
                        }).PersistDataToDirectory(new DirectoryInfo("/LettuceEncrypt"), null);
                    })
                    .ConfigureKestrel(options =>
                    {
                        options.Limits.MaxRequestBodySize = 1024;
                        options.Listen(ipEndPoint, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                            if (httpsOption.HasValue())
                            {
                                if (pfxOption.HasValue() && pfxpassOption.HasValue())
                                    listenOptions.UseHttps(pfxOption.Value(), pfxpassOption.Value());
                                else if (pfxOption.HasValue()) listenOptions.UseHttps(pfxOption.Value());
                                else listenOptions.UseHttps();
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
