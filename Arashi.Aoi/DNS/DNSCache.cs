using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Caching;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Http;

namespace Arashi
{
    public class DnsCache
    {
        public static void Add(DnsMessage dnsMessage)
        {
            dnsMessage.AuthorityRecords.RemoveAll(item =>
                item.Name.IsSubDomainOf(DomainName.Parse("arashi-msg")) ||
                item.Name.IsSubDomainOf(DomainName.Parse("nova-msg")));

            if (dnsMessage.AnswerRecords.Count <= 0) return;
            var dnsRecordBase = dnsMessage.AnswerRecords.FirstOrDefault();
            Add(new CacheItem($"DNS:{dnsRecordBase.Name}:{dnsRecordBase.RecordType}",
                    new CacheEntity
                    {
                        List = dnsMessage.AnswerRecords.ToList(),
                        Time = DateTime.Now,
                        ExpiresTime = DateTime.Now.AddSeconds(dnsRecordBase.TimeToLive)
                    }),
                dnsRecordBase.TimeToLive);
        }

        public static void Add(DnsMessage dnsMessage, HttpContext context)
        {
            dnsMessage.AuthorityRecords.RemoveAll(item =>
                item.Name.IsSubDomainOf(DomainName.Parse("arashi-msg")) ||
                item.Name.IsSubDomainOf(DomainName.Parse("nova-msg")));

            if (dnsMessage.AnswerRecords.Count <= 0) return;
            var dnsRecordBase = dnsMessage.AnswerRecords.FirstOrDefault();
            if (RealIP.TryGetFromDns(dnsMessage, out var ipAddress))
                Add(new CacheItem(
                        $"DNS:{GeoIP.GetGeoStr(ipAddress)}{dnsRecordBase.Name}:{dnsRecordBase.RecordType}",
                        new CacheEntity
                        {
                            List = dnsMessage.AnswerRecords.ToList(),
                            Time = DateTime.Now,
                            ExpiresTime = DateTime.Now.AddSeconds(dnsRecordBase.TimeToLive)
                        }),
                    dnsRecordBase.TimeToLive);
            else
                Add(new CacheItem($"DNS:{dnsRecordBase.Name}:{dnsRecordBase.RecordType}",
                        new CacheEntity
                        {
                            List = dnsMessage.AnswerRecords.ToList(),
                            Time = DateTime.Now,
                            ExpiresTime = DateTime.Now.AddSeconds(dnsRecordBase.TimeToLive)
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

        public static void Remove(DomainName name, RecordType type, IPAddress ip)
        {
            try
            {
                MemoryCache.Default.Remove($"DNS:{name}:{type}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            try
            {
                MemoryCache.Default.Remove($"DNS:{GeoIP.GetGeoStr(ip)}{name}:{type}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
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
            foreach (var item in cacheEntity.List)
            {
                if (item is ARecord aRecord)
                    dCacheMsg.AnswerRecords.Add(new ARecord(aRecord.Name,
                        Convert.ToInt32((cacheEntity.Time.AddSeconds(item.TimeToLive) - DateTime.Now)
                            .TotalSeconds), aRecord.Address));
                else if (item is AaaaRecord aaaaRecord)
                    dCacheMsg.AnswerRecords.Add(new AaaaRecord(aaaaRecord.Name,
                        Convert.ToInt32((cacheEntity.Time.AddSeconds(item.TimeToLive) - DateTime.Now)
                            .TotalSeconds), aaaaRecord.Address));
                else if (item is CNameRecord cNameRecord)
                    dCacheMsg.AnswerRecords.Add(new CNameRecord(cNameRecord.Name,
                        Convert.ToInt32((cacheEntity.Time.AddSeconds(item.TimeToLive) - DateTime.Now)
                            .TotalSeconds), cNameRecord.CanonicalName));
                else
                    dCacheMsg.AnswerRecords.Add(item);
            }

            dCacheMsg.Questions.AddRange(dnsQMessage.Questions);
            dCacheMsg.AuthorityRecords.Add(new TxtRecord(DomainName.Parse("cache.arashi-msg"), 0,
                cacheEntity.ExpiresTime.ToString("r")));
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
            public DateTime Time;
            public DateTime ExpiresTime;
        }
    }
}
