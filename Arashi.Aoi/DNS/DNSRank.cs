using System;
using System.Linq;
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using LiteDB;
using Microsoft.AspNetCore.Http;

namespace Arashi.Aoi.DNS
{
    public class DNSRank
    {
        public static LiteDatabase Database = new(@"rank.db");
        private static ILiteCollection<BsonDocument> collection = Database.GetCollection<BsonDocument>("FullRank");
        private static ILiteCollection<BsonDocument> geoCollection = Database.GetCollection<BsonDocument>("GeoRank");

        public static bool UseS3 = false;
        public static AmazonS3Client S3Client = new(
            "KeyID",
            "ApplicationKey",
            new AmazonS3Config
            {
                ServiceURL = "https://s3.us-west-000.backblazeb2.com/"
            }
        );

        public static void AddUp(DomainName name)
        {
            if (UseS3)
            {
                S3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = "arashi-logs",
                    Key = $"{name.ToString().TrimEnd('.')}/{DateTime.Now:yyyyMMddHHmm}-{Guid.NewGuid()}",
                    ContentType = "text/plain",
                    ContentBody = string.Empty
                });
                return;
            }
            var find = collection.Find(x => x["Name"] == name.ToString()).ToList();
            if (find.Any())
            {
                find.FirstOrDefault()["Count"] += 1;
                collection.Update(find.FirstOrDefault());
            }
            else
                collection.Insert(new BsonDocument { ["Name"] = name.ToString(), ["Count"] = 1 });
        }

        public static void AddUpGeo(DnsMessage dnsMessage, HttpContext context)
        {
            try
            {
                if (dnsMessage.AnswerRecords.Count <= 0) return;
                var name = dnsMessage.AnswerRecords.FirstOrDefault().Name;
                if (!RealIP.TryGetFromDns(dnsMessage, out var ipaddr)) ipaddr = RealIP.Get(context);
                if (Equals(ipaddr, IPAddress.Any) || IPAddress.IsLoopback(ipaddr)) return;
                var asn = GeoIP.AsnReader.Asn(ipaddr).AutonomousSystemNumber.ToString();
                var country = GeoIP.CityReader.City(ipaddr).Country.IsoCode;
                if (UseS3)
                {
                    S3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = "arashi-logs",
                        Key = $"{name.ToString().TrimEnd('.')}/{country}-{asn}/" +
                              $"{DateTime.Now:yyyyMMddHHmm}-{Guid.NewGuid()}",
                        ContentType = "text/plain",
                        ContentBody = string.Empty
                    });
                    return;
                }

                var find = geoCollection
                    .Find(x => x["Name"] == name.ToString() && x["ASN"] == asn && x["Country"] == country)
                    .ToList();
                if (find.Any())
                {
                    find.FirstOrDefault()["Count"] += 1;
                    geoCollection.Update(find.FirstOrDefault());
                }
                else
                    geoCollection.Insert(new BsonDocument
                        { ["Name"] = name.ToString(), ["Count"] = 1, ["ASN"] = asn, ["Country"] = country });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
