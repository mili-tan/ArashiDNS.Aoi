using System;
using System.Linq;
using System.Net;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using LiteDB;
using Microsoft.AspNetCore.Http;

namespace Arashi.Aoi.DNS
{
    class DNSRank
    {
        private static LiteDatabase database = new(@"rank.db");
        private static ILiteCollection<BsonDocument> collection = database.GetCollection<BsonDocument>("FullRank");
        private static ILiteCollection<BsonDocument> geoCollection = database.GetCollection<BsonDocument>("GeoRank");


        public static void AddUp(DomainName name)
        {
            var find = collection.Find(x => x["Name"] == name.ToString()).ToList();
            if (find.Any())
            {
                find.FirstOrDefault()["Count"] += 1;
                collection.Update(find.FirstOrDefault());
            }
            else
            {
                collection.Insert(new BsonDocument { ["Name"] = name.ToString(), ["Count"] = 1 });
            }
        }

        public static void AddUpGeo(DnsMessage dnsMessage, HttpContext context)
        {
            try
            {
                if (dnsMessage.AnswerRecords.Count <= 0) return;
                var name = dnsMessage.AnswerRecords.FirstOrDefault().Name;
                if (!RealIP.TryGetFromDns(dnsMessage, out var ipaddr)) ipaddr = RealIP.Get(context);
                if (string.IsNullOrWhiteSpace(ipaddr) || ipaddr == IPAddress.Any.ToString()) return;
                var asn = GeoIP.AsnReader.Asn(ipaddr).AutonomousSystemNumber.ToString();
                var country = GeoIP.CityReader.City(ipaddr).Country.IsoCode;
                var find = geoCollection
                    .Find(x => x["Name"] == name.ToString() && x["ASN"] == asn && x["Country"] == country)
                    .ToList();
                if (find.Any())
                {
                    find.FirstOrDefault()["Count"] += 1;
                    geoCollection.Update(find.FirstOrDefault());
                }
                else
                {
                    geoCollection.Insert(new BsonDocument
                        { ["Name"] = name.ToString(), ["Count"] = 1, ["ASN"] = asn, ["Country"] = country });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
