using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using static Arashi.AoiConfig;
using static Arashi.Aoi.Routes.DNSRoutes;

namespace Arashi.Aoi
{
    class ResolveRoutes
    {
        public static void ResolveRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map("/resolve", async context =>
            {
                var queryDictionary = context.Request.Query;
                if (context.Request.Method == "POST")
                    await ReturnContext(context, true,
                        DnsQuery(await DNSParser.FromPostByteAsync(context), context),
                        transId: true);
                else if (queryDictionary.ContainsKey("dns"))
                    await ReturnContext(context, true,
                        DnsQuery(DNSParser.FromWebBase64(context), context),
                        transId: true);
                else if (queryDictionary.ContainsKey("name"))
                    await ReturnContext(context, false,
                        DnsQuery(DNSParser.FromDnsJson(context, EcsDefaultMask: Config.EcsDefaultMask), context),
                        transId: true);
                else
                    await context.WriteResponseAsync(Startup.IndexStr, type: "text/html");
            });
        }
    }
}
