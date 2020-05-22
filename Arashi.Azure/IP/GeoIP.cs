using System;
using System.Net;
using System.Threading.Tasks;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;

namespace Arashi
{
    public class GeoIP
    {
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        public static DatabaseReader AsnReader = new DatabaseReader(SetupBasePath + "GeoLite2-ASN.mmdb");
        public static DatabaseReader CityReader = new DatabaseReader(SetupBasePath + "GeoLite2-City.mmdb");
        public static AsnResponse GetAsnResponse(string ipAddress) => AsnReader.Asn(ipAddress);
        public static CityResponse GetCityResponse(string ipAddress) => CityReader.City(ipAddress);

        public static (AsnResponse, CityResponse) GetAsnCityValueTuple(string ipAddress)
        {
            var asn = new AsnResponse();
            var city = new CityResponse();
            Task.WaitAll(
                Task.Run(() => asn = GetAsnResponse(ipAddress)),
                Task.Run(() => city = GetCityResponse(ipAddress)));
            return (asn, city);
        }

        public static string GetCnISP(AsnResponse asnResponse, CityResponse cityResponse)
        {
            var country = cityResponse.Country.IsoCode;
            var asName = asnResponse.AutonomousSystemOrganization.ToLower();

            if (country != "CN") return string.Empty;

            if (asName.Contains("cernet") || asName.Contains("education") || asName.Contains("research") ||
                asName.Contains("university") || asName.Contains("academy") ||
                asName.Contains("computer network information center"))
                return "CE";
            if (asName.Contains("mobile") || asName.Contains("cmnet") || asName.Contains("tietong") ||
                asName.Contains("railway"))
                return "CM";
            if (asName.Contains("unicom") || asName.Contains("cnc") ||
                asName.Contains("china169") || asName.Contains("netcom"))
                return "CU";
            if (asName.Contains("chinanet") || asName.Contains("telecom") || asName.Contains("no.31,jin-rong") ||
                asName.Contains("inter-exchange") || asName.Contains("ct"))
                return "CT";

            return string.Empty;
        }

        public static string GetGeoStr(IPAddress ipAddress)
        {
            var i = GetAsnCityValueTuple(ipAddress.ToString());
            var cnisp = GetCnISP(i.Item1, i.Item2);
            return string.IsNullOrEmpty(cnisp)
                ? $"{i.Item2.Country.IsoCode}:{i.Item1.AutonomousSystemNumber}"
                : $"{i.Item2.Country.IsoCode}:{i.Item2.MostSpecificSubdivision.IsoCode}:{cnisp}";
        }
    }
}
