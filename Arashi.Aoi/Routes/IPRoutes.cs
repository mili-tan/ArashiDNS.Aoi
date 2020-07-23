using Arashi.Azure;
using Arashi.Kestrel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json.Linq;

namespace Arashi.Aoi.Routes
{
    class IPRoutes
    {
        public static void GeoIPRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.IpPerfix, async context =>
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(RealIP.Get(context).ToString());
            });
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
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(jObject.ToString());
            });
            endpoints.Map(Config.IpPerfix + "/json", async context =>
            {
                var jObject = new JObject();
                var asnCity = GeoIP.GetAsnCityValueTuple(context.Request.Query.ContainsKey("ip")
                    ? context.Request.Query["ip"].ToString()
                    : RealIP.Get(context));
                var responseAsn = asnCity.Item1;
                var responseCity = asnCity.Item2;
                jObject.Add("IP", responseAsn.IPAddress);
                jObject.Add("ASN", responseAsn.AutonomousSystemNumber);
                jObject.Add("Organization", responseAsn.AutonomousSystemOrganization);
                jObject.Add("CountryCode", responseCity.Country.IsoCode);
                jObject.Add("Country", responseCity.Country.Name);
                if (!string.IsNullOrWhiteSpace(responseCity.MostSpecificSubdivision.IsoCode))
                    jObject.Add("ProvinceCode", responseCity.MostSpecificSubdivision.IsoCode);
                if (!string.IsNullOrWhiteSpace(responseCity.MostSpecificSubdivision.Name))
                    jObject.Add("Province", responseCity.MostSpecificSubdivision.Name);
                if (!string.IsNullOrWhiteSpace(responseCity.City.Name))
                    jObject.Add("City", responseCity.City.Name);
                var cnIsp = GeoIP.GetCnISP(responseAsn, responseCity);
                if (!string.IsNullOrWhiteSpace(cnIsp)) jObject.Add("ISP", cnIsp);

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(jObject.ToString());
            });
        }
    }
}
