using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ArashiDNS.Tools;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;

namespace Arashi
{
    public class DNSChina
    {
        public static void Init()
        {
            try
            {
                if (!File.Exists(DNSChinaConfig.Config.ChinaListPath)) return;
                Parallel.ForEachAsync(File.ReadAllLines(DNSChinaConfig.Config.ChinaListPath), async (item, _) => await AddTask(item));
                GC.Collect();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static async Task AddTask(string item)
        {
            try
            {
                if (item.StartsWith("#")) return;
                await MFaster.FasterKv.UpsertAsync("CN:" + item.Trim('.'), "1");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static async Task<bool> IsChinaNameAsync(DomainName name)
        {
            try
            {
                if (DNSChinaConfig.NoCnDomains.Any(name.IsEqualOrSubDomainOf))
                    return false;
                if (name.IsSubDomainOf(DomainName.Parse("cn")))
                    return true;

                return
                    (await MFaster.FasterKv.ReadAsync("CN:" + name.ToString().TrimEnd('.'))).Item1.Found ||
                    (await MFaster.FasterKv.ReadAsync("CN:" + string.Join(".", name.Labels.TakeLast(2)))).Item1.Found;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public static async Task<DnsMessage> ResolveOverChinaDns(DnsMessage dnsMessage)
        {

            if (!DNSChinaConfig.Config.UseHttpDns)
                return await new DnsClient(IPAddress.Parse(DNSChinaConfig.Config.ChinaUpStream), AoiConfig.Config.TimeOut)
                    .SendMessageAsync(dnsMessage);

            var domainName = dnsMessage.Questions.FirstOrDefault()?.Name.ToString().TrimEnd('.');
            var dnsStr = string.Empty;

            try
            {
                var client = Startup.ClientFactory.CreateClient("DNSChina");
                var ip = RealIP.GetFromDns(dnsMessage);
                dnsStr = !Equals(ip, IPAddress.Any)
                    ? await client.GetStringAsync(
                        string.Format(DNSChinaConfig.Config.HttpDnsEcsUrl, domainName, ip))
                    : await client.GetStringAsync(
                        string.Format(DNSChinaConfig.Config.HttpDnsUrl, domainName));
            }
            catch (Exception)
            {
                var res = await new ARSoft.Tools.Net.Dns.DnsClient(IPAddress.Parse(DNSChinaConfig.Config.ChinaUpStream),
                        AoiConfig.Config.TimeOut)
                    .SendMessageAsync(dnsMessage);

                if (res != null && res.AnswerRecords.Any())
                    return res;
            }

            if (string.IsNullOrWhiteSpace(dnsStr)) throw new TimeoutException();

            var dnsAMessage = new DnsMessage
            {
                IsRecursionAllowed = true,
                IsRecursionDesired = true,
                TransactionID = dnsMessage.TransactionID,
            };

            if (dnsStr.StartsWith("0,") || dnsStr.StartsWith("0.0.0.1,"))
            {
                dnsAMessage.AnswerRecords = new List<DnsRecordBase>();
                dnsAMessage.ReturnCode = ReturnCode.NxDomain;
            }
            else
            {
                var ttlTime = Convert.ToInt32(dnsStr.Split(',')[1]);
                var dnsAnswerList = dnsStr.Split(',')[0].Split(';');

                dnsAMessage.AnswerRecords = new List<DnsRecordBase>(dnsAnswerList
                    .Select(item => new ARecord(DomainName.Parse(domainName), ttlTime, IPAddress.Parse(item)))
                    .Cast<DnsRecordBase>().ToList());
            }

            dnsAMessage.Questions.AddRange(dnsMessage.Questions);
            try
            {
                dnsAMessage.IsEDnsEnabled = true;
                if (RealIP.TryGetFromDns(dnsMessage, out var ecs))
                    dnsAMessage.EDnsOptions.Options.Add(new ClientSubnetOption(24, ecs));
            }
            catch (Exception)
            {
                // ignored
            }

            dnsAMessage.AuthorityRecords.Add(new TxtRecord(DomainName.Parse("china.arashi-msg"), 0,
                "ArashiDNS.P ChinaList"));
            return dnsAMessage;
        }
    }
}
