using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Arashi.Azure
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AoiConfig.Config = new AoiConfig();
            File.WriteAllText("Token.txt", AoiConfig.Config.AdminToken);
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(
                webBuilder => webBuilder.UseStartup<Startup>());
    }
}
