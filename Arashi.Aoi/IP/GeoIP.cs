using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;

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
                    return string.Empty;
                if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
                    return "IPV6:";
                var (asnResponse, cityResponse) = GetAsnCityValueTuple(ipAddress);
                var cnIsp = GetCnISP(asnResponse, cityResponse);
                if (!string.IsNullOrEmpty(cnIsp))
                    return
                        $"{cityResponse.Country.IsoCode ?? "UN"}:{cityResponse.MostSpecificSubdivision.IsoCode ?? "UN"}:{cnIsp}:";
                if (cityResponse.Country.IsoCode is "CN" or "US" or "CA" or "RU" or "AU" &&
                    !string.IsNullOrWhiteSpace(cityResponse.MostSpecificSubdivision.IsoCode))
                    return
                        $"{cityResponse.Country.IsoCode ?? "UN"}:{cityResponse.MostSpecificSubdivision.IsoCode ?? "UN"}:" +
                        $"{asnResponse.AutonomousSystemNumber}:";

                return $"{cityResponse.Country.IsoCode ?? "UN"}:{asnResponse.AutonomousSystemNumber}:";
            }
            catch (Exception)
            {
                return Convert.ToBase64String(ipAddress.GetAddressBytes()) + ":";
            }
        }

        public static string GetGeoFullStr(IPAddress ipAddress)
        {
            try
            {
                if (IPAddress.IsLoopback(ipAddress) || Equals(ipAddress, IPAddress.Any))
                    return string.Empty;
                if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
                    return "IPv6";
                var (asnResponse, cityResponse) = GetAsnCityValueTuple(ipAddress);
                if (!string.IsNullOrWhiteSpace(cityResponse.MostSpecificSubdivision.Name))
                    return $"{cityResponse.Country.IsoCode} {cityResponse.MostSpecificSubdivision.Name} - " +
                           $"{asnResponse.AutonomousSystemOrganization}";

                return $"{cityResponse.Country.IsoCode} - {asnResponse.AutonomousSystemOrganization}";
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }
    }
}
