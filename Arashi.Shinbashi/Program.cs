using System;
using Arashi.Azure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Arashi.Shinbashi
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(AppDomain.CurrentDomain.SetupInformation.ApplicationBase)
                .ConfigureServices(services => services.AddRouting())
                .ConfigureKestrel(options =>
                {
                    options.Limits.MaxRequestBodySize = 1024;
                    options.ListenLocalhost(2020, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    });
                })
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
