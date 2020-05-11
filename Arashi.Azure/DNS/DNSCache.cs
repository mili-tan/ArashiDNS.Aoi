using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;

namespace Arashi
{
    public class DnsCache
    {
        public static void Add(DnsMessage dnsMessage)
        {
            if (dnsMessage.AnswerRecords.Count <= 0) return;
            var dnsRecordBase = dnsMessage.AnswerRecords.FirstOrDefault();
            var cacheItem = new CacheItem($"{dnsRecordBase.Name}:{dnsRecordBase.RecordType}",
                dnsMessage.AnswerRecords.ToList());
            if (!MemoryCache.Default.Contains(cacheItem.Key))
                MemoryCache.Default.Add(cacheItem,
                    new CacheItemPolicy
                    {
                        AbsoluteExpiration =
                            DateTimeOffset.Now + TimeSpan.FromSeconds(dnsRecordBase.TimeToLive)
                    });
        }

        public static bool Contains(DnsMessage dnsQMsg)
        {
            return MemoryCache.Default.Contains(
                $"{dnsQMsg.Questions.FirstOrDefault().Name}:{dnsQMsg.Questions.FirstOrDefault().RecordType}");
        }

        public static DnsMessage Get(DnsMessage dnsQMessage)
        {
            var dCacheMsg = new DnsMessage
            {
                IsRecursionAllowed = true,
                IsRecursionDesired = true,
                TransactionID = dnsQMessage.TransactionID
            };
            dCacheMsg.AnswerRecords.AddRange(
                (List<DnsRecordBase>) MemoryCache.Default.Get(
                    $"{dnsQMessage.Questions.FirstOrDefault().Name}:{dnsQMessage.Questions.FirstOrDefault().RecordType}") ??
                throw new InvalidOperationException());
            dCacheMsg.Questions.AddRange(dnsQMessage.Questions);
            dCacheMsg.AnswerRecords.Add(new TxtRecord(DomainName.Parse("cache.doh.pp.ua"), 0,
                "ArashiDNS.P Cached"));
            return dCacheMsg;
        }
    }
}
