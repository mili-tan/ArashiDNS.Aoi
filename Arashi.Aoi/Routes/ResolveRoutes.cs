using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using static Arashi.AoiConfig;
using static Arashi.Aoi.Routes.DnsQueryRoutes;

namespace Arashi.Aoi
{
    class ResolveRoutes
    {
        public static void ResolveRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map("/resolve", async context =>
            {
                var idEnable = Config.TransIdEnable;
                var queryDictionary = context.Request.Query;

                if (context.Request.Method == "POST")
                {
                    var dnsq = await DNSParser.FromPostByteAsync(context);
                    await ReturnContext(context, true,
                        DnsQuery(dnsq, context),
                        transIdEnable: idEnable, id: dnsq.TransactionID);
                }
                else if (queryDictionary.ContainsKey("dns"))
                {
                    var dnsq = DNSParser.FromWebBase64(context);
                    await ReturnContext(context, true,
                        DnsQuery(dnsq, context),
                        transIdEnable: idEnable, id: dnsq.TransactionID);
                }
                else if (queryDictionary.ContainsKey("name"))
                    await ReturnContext(context, false,
                        DnsQuery(DNSParser.FromDnsJson(context, EcsDefaultMask: Config.EcsDefaultMask), context),
                        transIdEnable: idEnable);
                else
                    await context.WriteResponseAsync(Startup.IndexStr, type: "text/html");
            });
        }
    }
}
