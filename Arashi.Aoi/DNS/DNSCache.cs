using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using Arashi.Kestrel;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Http;

namespace Arashi
{
    public class DnsCache
    {
        public static void Add(DnsMessage dnsMessage)
        {
            if (dnsMessage.AnswerRecords.Count <= 0) return;
            var dnsRecordBase = dnsMessage.AnswerRecords.FirstOrDefault();
            Add(new CacheItem($"DNS:{dnsRecordBase.Name}:{dnsRecordBase.RecordType}",
                    new CacheEntity
                    {
                        List = dnsMessage.AnswerRecords.ToList(),
                        ExpiredTime = DateTime.Now.AddSeconds(dnsRecordBase.TimeToLive)
                    }),
                dnsRecordBase.TimeToLive);
        }

        public static void Add(DnsMessage dnsMessage, HttpContext context)
        {
            if (dnsMessage.AnswerRecords.Count <= 0) return;
            var dnsRecordBase = dnsMessage.AnswerRecords.FirstOrDefault();
            if (RealIP.TryGetFromDns(dnsMessage, out var ipAddress))
                Add(new CacheItem(
                        $"DNS:{GeoIP.GetGeoStr(ipAddress)}{dnsRecordBase.Name}:{dnsRecordBase.RecordType}",
                        new CacheEntity
                        {
                            List = dnsMessage.AnswerRecords.ToList(),
                            ExpiredTime = DateTime.Now.AddSeconds(dnsRecordBase.TimeToLive)
                        }),
                    dnsRecordBase.TimeToLive);
            else
                Add(new CacheItem($"DNS:{dnsRecordBase.Name}:{dnsRecordBase.RecordType}",
                        new CacheEntity
                        {
                            List = dnsMessage.AnswerRecords.ToList(),
                            ExpiredTime = DateTime.Now.AddSeconds(dnsRecordBase.TimeToLive)
                        }),
                    dnsRecordBase.TimeToLive);
        }

        public static void Add(CacheItem cacheItem, int ttl)
        {
            if (!MemoryCache.Default.Contains(cacheItem.Key))
                MemoryCache.Default.Add(cacheItem,
                    new CacheItemPolicy
                    {
                        AbsoluteExpiration =
                            DateTimeOffset.Now + TimeSpan.FromSeconds(ttl)
                    });
        }

        public static bool Contains(DnsMessage dnsQMsg, HttpContext context = null)
        {
            return context == null
                ? MemoryCache.Default.Contains(
                    $"DNS:{dnsQMsg.Questions.FirstOrDefault().Name}:{dnsQMsg.Questions.FirstOrDefault().RecordType}")
                : MemoryCache.Default.Contains(
                    $"DNS:{GeoIP.GetGeoStr(RealIP.GetFromDns(dnsQMsg, context))}{dnsQMsg.Questions.FirstOrDefault().Name}:{dnsQMsg.Questions.FirstOrDefault().RecordType}");
        }

        public static DnsMessage Get(DnsMessage dnsQMessage, HttpContext context = null)
        {
            var dCacheMsg = new DnsMessage
            {
                IsRecursionAllowed = true,
                IsRecursionDesired = true,
                TransactionID = dnsQMessage.TransactionID
            };
            var getName = context != null
                ? $"DNS:{GeoIP.GetGeoStr(RealIP.GetFromDns(dnsQMessage, context))}{dnsQMessage.Questions.FirstOrDefault().Name}:{dnsQMessage.Questions.FirstOrDefault().RecordType}"
                : $"DNS:{dnsQMessage.Questions.FirstOrDefault().Name}:{dnsQMessage.Questions.FirstOrDefault().RecordType}";
            var cacheEntity = Get(getName);
            dCacheMsg.AnswerRecords.AddRange(cacheEntity.List);
            dCacheMsg.Questions.AddRange(dnsQMessage.Questions);
            dCacheMsg.AnswerRecords.Add(new TxtRecord(DomainName.Parse("cache.arashi-msg"), 0,
                "ArashiDNS.P Cached"));
            dCacheMsg.AnswerRecords.Add(new TxtRecord(DomainName.Parse("cache.expired"), 0,
                cacheEntity.ExpiredTime.ToString(CultureInfo.CurrentCulture)));
            return dCacheMsg;
        }

        public static CacheEntity Get(string key)
        {
            return (CacheEntity) MemoryCache.Default.Get(key) ??
                   throw new InvalidOperationException();
        }

        public class CacheEntity
        {
            public List<DnsRecordBase> List;
            public DateTime ExpiredTime;
        }
    }
}
