using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.Extensions.DependencyInjection;

namespace Arashi
{
    public class DNSChina
    {
        public static List<DomainName> ChinaList = new();
        public static IServiceProvider ServiceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        public static IHttpClientFactory ClientFactory = ServiceProvider.GetService<IHttpClientFactory>();

        public static bool IsChinaName(DomainName name)
        {
            return ChinaList.Any(name.IsEqualOrSubDomainOf);
        }

        public static async Task<DnsMessage> ResolveOverChinaDns(DnsMessage dnsMessage)
        {

            if (!DNSChinaConfig.Config.UseHttpDns)
                return await new ARSoft.Tools.Net.Dns.DnsClient(IPAddress.Parse(DNSChinaConfig.Config.ChinaUpStream), AoiConfig.Config.TimeOut)
                    .SendMessageAsync(dnsMessage);

            var domainName = dnsMessage.Questions.FirstOrDefault()?.Name.ToString().TrimEnd('.');
            var dnsStr = string.Empty;

            try
            {
                var client = ClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(1);

                if (dnsMessage.IsEDnsEnabled)
                {
                    foreach (var eDnsOptionBase in dnsMessage.EDnsOptions.Options.ToArray())
                        if (eDnsOptionBase is ClientSubnetOption option)
                        {
                            dnsStr = await client.GetStringAsync(
                                string.Format(DNSChinaConfig.Config.HttpDnsEcsUrl, domainName, option.Address));
                            break;
                        }
                }
                else
                {

                    dnsStr = await client.GetStringAsync(
                        string.Format(DNSChinaConfig.Config.HttpDnsUrl, domainName));
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
