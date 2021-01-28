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
                    {"IP", RealIP.Get(context)}, {"UserHostAddress", context.Connection.RemoteIpAddress.ToString()}
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
                var (responseAsn, responseCity) = GeoIP.GetAsnCityValueTuple(context.Request.Query.ContainsKey("ip")
                    ? context.Request.Query["ip"].ToString()
                    : RealIP.Get(context));
                var jObject = new JObject
                {
                    {"IP", responseAsn.IPAddress},
                    {"ASN", responseAsn.AutonomousSystemNumber},
                    {"Organization", responseAsn.AutonomousSystemOrganization},
                    {"CountryCode", responseCity.Country.IsoCode},
                    {"Country", responseCity.Country.Name}
                };
                if (!string.IsNullOrWhiteSpace(responseCity.MostSpecificSubdivision.IsoCode))
                    jObject.Add("ProvinceCode", responseCity.MostSpecificSubdivision.IsoCode);
                if (!string.IsNullOrWhiteSpace(responseCity.MostSpecificSubdivision.Name))
                    jObject.Add("Province", responseCity.MostSpecificSubdivision.Name);
                if (!string.IsNullOrWhiteSpace(responseCity.City.Name))
                    jObject.Add("City", responseCity.City.Name);
                var cnIsp = GeoIP.GetCnISP(responseAsn, responseCity);
                if (!string.IsNullOrWhiteSpace(cnIsp)) jObject.Add("ISP", cnIsp);
                await context.WriteResponseAsync(jObject.ToString(), type: "application/json");
            });
        }
    }
}
