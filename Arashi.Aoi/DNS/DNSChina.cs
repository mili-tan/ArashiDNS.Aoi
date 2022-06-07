using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;

namespace Arashi
{
    public class DNSChina
    {
        public static List<DomainName> ChinaList = File.Exists(DNSChinaConfig.Config.ChinaListPath)
            ? File.ReadAllLines(DNSChinaConfig.Config.ChinaListPath).ToList().ConvertAll(DomainName.Parse)
            : new List<DomainName>();

        public static bool IsChinaName(DomainName name) => ChinaList.Any(name.IsEqualOrSubDomainOf);

        public static DnsMessage ResolveOverChinaDns(DnsMessage dnsMessage)
        {
            if (!DNSChinaConfig.Config.UseHttpDns)
                return new ARSoft.Tools.Net.Dns.DnsClient(IPAddress.Parse(DNSChinaConfig.Config.ChinaUpStream), AoiConfig.Config.TimeOut)
                    .SendMessage(dnsMessage);
            var domainName = dnsMessage.Questions.FirstOrDefault()?.Name.ToString().TrimEnd('.');
            var dnsStr = string.Empty;

            try
            {
                if (dnsMessage.IsEDnsEnabled)
                {
                    foreach (var eDnsOptionBase in dnsMessage.EDnsOptions.Options.ToArray())
                        if (eDnsOptionBase is ClientSubnetOption option)
                        {
                            var task = new HttpClient().GetStringAsync(
                                string.Format(DNSChinaConfig.Config.HttpDnsEcsUrl, domainName, option.Address));
                            task.Wait(1000);
                            dnsStr = task.Result;
                            break;
                        }
                }
                else
                {
                    var task = new HttpClient().GetStringAsync(
                        string.Format(DNSChinaConfig.Config.HttpDnsUrl, domainName));
                    task.Wait(1000);
                    dnsStr = task.Result;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                DNSChinaConfig.Config.HttpDnsUrl = DNSChinaConfig.BackupHttpDnsUrl;
                DNSChinaConfig.Config.HttpDnsEcsUrl = DNSChinaConfig.BackupHttpDnsEcsUrl;
            }

            if (string.IsNullOrWhiteSpace(dnsStr)) throw new TimeoutException();
            var ttlTime = Convert.ToInt32(dnsStr.Split(',')[1]);
            var dnsAnswerList = dnsStr.Split(',')[0].Split(';');
            var dnsAMessage = new DnsMessage
            {
                IsRecursionAllowed = true,
                IsRecursionDesired = true,
                TransactionID = dnsMessage.TransactionID,
                AnswerRecords = new List<DnsRecordBase>(dnsAnswerList
                    .Select(item => new ARecord(DomainName.Parse(domainName), ttlTime, IPAddress.Parse(item)))
                    .Cast<DnsRecordBase>().ToList())
            };
            dnsAMessage.Questions.AddRange(dnsMessage.Questions);
            dnsAMessage.AnswerRecords.Add(new TxtRecord(DomainName.Parse("china.arashi-msg"), 0,
                "ArashiDNS.P ChinaList"));
            return dnsAMessage;
        }
    }
}
