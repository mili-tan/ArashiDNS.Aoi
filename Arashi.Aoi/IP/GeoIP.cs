using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;
using Newtonsoft.Json.Linq;

namespace Arashi
{
    public class GeoIP
    {
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        public static DatabaseReader AsnReader;
        public static DatabaseReader CityReader;
        public static AsnResponse GetAsnResponse(IPAddress ipAddress) => AsnReader.Asn(ipAddress);
        public static CityResponse GetCityResponse(IPAddress ipAddress) => CityReader.City(ipAddress);
        public static void Init()
        {
            try
            {
                AsnReader = new DatabaseReader(SetupBasePath + "GeoLite2-ASN.mmdb");
                CityReader = new DatabaseReader(SetupBasePath + "GeoLite2-City.mmdb");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static long GetAsnNumber(IPAddress ipAddress)
        {
            return GetAsnResponse(ipAddress).AutonomousSystemNumber ?? 0;
        }

        public static (AsnResponse, CityResponse) GetAsnCityValueTuple(IPAddress ipAddress)
        {
            var asn = new AsnResponse();
            var city = new CityResponse();
            try
            {
                Parallel.Invoke(() => asn = GetAsnResponse(ipAddress),
                    () => city = GetCityResponse(ipAddress));
            }
            catch (Exception)
            {
                // ignored
            }
            return (asn, city);
        }

        public static string GetCnISP(AsnResponse asnResponse, CityResponse cityResponse)
        {
            var country = cityResponse.Country.IsoCode ?? string.Empty;
            if (country != "CN") return string.Empty;

            var asName = (asnResponse.AutonomousSystemOrganization ?? string.Empty).ToLower();

            if (asName == string.Empty) return "UN";

            if (asName.Contains("cernet") || asName.Contains("education") || asName.Contains("research") ||
                asName.Contains("university") || asName.Contains("academy") ||
                asName.Contains("computer network information center"))
                return "CE";
            if (asName.Contains("mobile") || asName.Contains("cmnet") || asName.Contains("tietong") ||
                asName.Contains("railway") || asName.Contains("railcom"))
                return "CM";
            if (asName.Contains("unicom") || asName.Contains("cnc") ||
                asName.Contains("china169") || asName.Contains("netcom"))
                return "CU";
            if (asName.Contains("chinanet") || asName.Contains("telecom") || asName.Contains("no.31,jin-rong") ||
                asName.Contains("inter-exchange") || asName.Contains("ct"))
                return "CT";

            return "UN";
        }

        public static string GetGeoStr(IPAddress ipAddress)
        {
            try
            {
                if (IPAddress.IsLoopback(ipAddress) || Equals(ipAddress, IPAddress.Any))
                    return "ANY:";
                var (asnResponse, cityResponse) = GetAsnCityValueTuple(ipAddress);
                var cnIsp = GetCnISP(asnResponse, cityResponse);
                if (!string.IsNullOrEmpty(cnIsp))
                    return
                        $"{cityResponse.Country.IsoCode}:{cityResponse.MostSpecificSubdivision.IsoCode ?? "UN"}:{cnIsp}:";
                if (!string.IsNullOrWhiteSpace(cityResponse.MostSpecificSubdivision.IsoCode) &&
                    cityResponse.Country.IsoCode is "CN" or "US" or "CA" or "RU" or "AU")
                    return
                        $"{cityResponse.Country.IsoCode ?? "UN"}:{cityResponse.MostSpecificSubdivision.IsoCode ?? "UN"}:" +
                        $"{asnResponse.AutonomousSystemNumber ?? 0}:";

                return $"{cityResponse.Country.IsoCode ?? "UN"}:{asnResponse.AutonomousSystemNumber ?? 0}:";
            }
            catch (Exception)
            {
                return Convert.ToBase64String(ipAddress.GetAddressBytes()).Trim('=') + ":";
            }
        }

        public static string GetGeoFullStr(IPAddress ipAddress)
        {
            try
            {
                if (IPAddress.IsLoopback(ipAddress) || Equals(ipAddress, IPAddress.Any))
                    return string.Empty;
                var (asnResponse, cityResponse) = GetAsnCityValueTuple(ipAddress);
                if (!string.IsNullOrWhiteSpace(cityResponse.MostSpecificSubdivision.Name))
                    return $"{cityResponse.Country.IsoCode ?? "UN"} {cityResponse.MostSpecificSubdivision.Name ?? "UN"} - " +
                           $"{asnResponse.AutonomousSystemOrganization ?? "Unknown"}";

                return $"{cityResponse.Country.IsoCode ?? "UN"} - {asnResponse.AutonomousSystemOrganization ?? "Unknown"}";
            }
            catch (Exception)
            {
                try
                {
                    return GetAsnFromRipeStat(ipAddress).Result.Name;
                }
                catch (Exception)
                {
                    return "Unknown";
                }
            }
        }

        public static async Task<(long Asn, string Name)> GetAsnFromRipeStat(IPAddress ipAddress)
        {
            try
            {
                var asns = JObject.Parse(await Startup.ClientFactory.CreateClient("GET")
                    .GetStringAsync(
                        "https://stat.ripe.net/data/prefix-overview/data.json?data_overload_limit=ignore&min_peers_seeing=10&resource=" +
                        ipAddress))["data"]?["asns"].FirstOrDefault();

                var res = (long.Parse(asns["asn"].ToString()), asns["holder"].ToString());
                return res;
            }
            catch
            {
                return (0, "Unknown");
            }
        }
    }
}
