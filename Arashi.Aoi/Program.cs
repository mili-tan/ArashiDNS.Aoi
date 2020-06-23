using System;
using System.Net;
using Arashi.Azure;
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
                {Name = "Arashi.Aoi", Description = "ArashiDNS.Aoi - Simple Lightweight DNS over HTTPS Server "};

            cmd.HelpOption("-?|-h|--help");
            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>", "Set listen address and port",
                CommandOptionType.SingleValue);

            cmd.OnExecute(() =>
            {
                var ipEndPoint = ipOption.HasValue()
                    ? IPEndPoint.Parse(ipOption.Value())
                    : new IPEndPoint(IPAddress.Loopback, 2020);

                var host = new WebHostBuilder()
                    .UseKestrel()
                    .UseContentRoot(AppDomain.CurrentDomain.SetupInformation.ApplicationBase)
                    .ConfigureServices(services => services.AddRouting())
                    .ConfigureKestrel(options =>
                    {
                        options.Limits.MaxRequestBodySize = 1024;
                        options.Listen(ipEndPoint, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
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
