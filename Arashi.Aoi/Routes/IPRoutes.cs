using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json.Linq;
using static Arashi.AoiConfig;

namespace Arashi.Aoi.Routes
{
    class IPRoutes
    {
        public static void GeoIPRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.IpPerfix,
                async context => await context.WriteResponseAsync(RealIP.Get(context).ToString()));
            endpoints.Map(Config.IpPerfix + "/source", async context =>
            {
                var jObject = new JObject
                {
                    {"IP", RealIP.Get(context).ToString()}, {"UserHostAddress", context.Connection.RemoteIpAddress.ToString()}
                };
                if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var xFwdValue))
                    jObject.Add("X-Forwarded-For", xFwdValue.ToString());
                if (context.Request.Headers.TryGetValue("CF-Connecting-IP", out var xCfValue))
                    jObject.Add("CF-Connecting-IP", xCfValue.ToString());
                if (context.Request.Headers.TryGetValue("X-Real-IP", out var xRealValue))
                    jObject.Add("X-Real-IP", xRealValue.ToString());
                await context.WriteResponseAsync(jObject.ToString(), type: "application/json");
            });
            endpoints.Map(Config.IpPerfix + "/json", async context =>
            {
                var ip = context.Request.Query.ContainsKey("ip")
                    ? IPAddress.Parse(context.Request.Query["ip"].ToString())
                    : RealIP.Get(context);
                try
                {
                    var ripe = ((long)0, "");
                    var (responseAsn, responseCity) = GeoIP.GetAsnCityValueTuple(ip);
                    if (responseAsn.AutonomousSystemNumber == null) ripe = await GeoIP.GetAsnFromRipeStat(ip);

                    var jObject = new JObject
                    {
                        {"IP", responseAsn.IPAddress ?? ip.ToString()},
                        {"ASN", responseAsn.AutonomousSystemNumber ?? ripe.Item1},
                        {"Organization", responseAsn.AutonomousSystemOrganization ?? ripe.Item2},
                        {"CountryCode", responseCity.Country.IsoCode ?? "UN"},
                        {"Country", responseCity.Country.Name ?? "Unknown"}
                    };
                    if (!string.IsNullOrWhiteSpace(responseCity.MostSpecificSubdivision.IsoCode))
                        jObject.Add("ProvinceCode", responseCity.MostSpecificSubdivision.IsoCode);
                    if (!string.IsNullOrWhiteSpace(responseCity.MostSpecificSubdivision.Name))
                        jObject.Add("Province", responseCity.MostSpecificSubdivision.Name);
                    if (!string.IsNullOrWhiteSpace(responseCity.City.Name))
                        jObject.Add("City", responseCity.City.Name);
                    await context.WriteResponseAsync(jObject.ToString(), type: "application/json");
                }
                catch (Exception e)
                {
                    var ripe = await GeoIP.GetAsnFromRipeStat(ip);
                    var jObject = new JObject
                    {
                        {"IP", ip.ToString()},
                        {"ASN", ripe.Asn},
                        {"Organization", ripe.Name}
                    };
                    await context.WriteResponseAsync(jObject.ToString(), type: "application/json");
                }
            });
        }
    }
}
