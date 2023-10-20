#nullable enable
using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Arashi;
using Newtonsoft.Json.Linq;

namespace ArashiDNS.P2.IP
{
    internal class ReplaceIP
    {
        public static Hashtable CnIpHashtable = new();

        public static void Init()
        {
            //            CnIpHashtable.Add($"CN:{split[0]}:{split[1]}".ToUpper(), IPAddress.Parse(split.LastOrDefault() ?? ""));
        }

        public static async Task<IPAddress> Get(IPAddress ipAddress)
        {
            try
            {
                if (IPAddress.IsLoopback(ipAddress) || Equals(ipAddress, IPAddress.Any))
                    return ipAddress;
                var (asnResponse, cityResponse) = GeoIP.GetAsnCityValueTuple(ipAddress);
                var asn = asnResponse.AutonomousSystemNumber ?? (await GeoIP.GetAsnFromRipeStat(ipAddress)).Asn;
                var country = cityResponse.Country.IsoCode ?? "UN";
                var netResult = await GetFromAsnc(asn, country) ?? await GetFromRipeStat(asn, country) ?? ipAddress;
                return netResult;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return ipAddress;
            }
        }

        public static async Task<IPAddress?> GetFromAsnc(long asn, string country)
        {
            try
            {
                return IPNetwork
                    .Parse(JArray
                        .Parse(await Startup.ClientFactory.CreateClient("GET")
                            .GetStringAsync($"https://asn.novaxns.workers.dev/api/asnc/get?as={asn}&geo={country}"))
                        .First?["net"]?.ToString()).FirstUsable;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<IPAddress?> GetFromRipeStat(long asn, string country)
        {
            try
            {
                var resources = JObject.Parse(await Startup.ClientFactory.CreateClient("GET")
                    .GetStringAsync(
                        "https://stat.ripe.net/data/maxmind-geo-lite-announced-by-as/data.json?data_overload_limit=ignore&resource=" +
                        asn))["data"]?["located_resources"];

                if (country == "UN" && resources != null)
                    return IPNetwork.Parse(resources.FirstOrDefault()?["resource"]?.ToString()).FirstUsable;

                return (from item in resources
                    where item["locations"].FirstOrDefault()["country"].ToString() == country
                    select IPNetwork.Parse(item["resource"].ToString()).FirstUsable).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }
    }
}
