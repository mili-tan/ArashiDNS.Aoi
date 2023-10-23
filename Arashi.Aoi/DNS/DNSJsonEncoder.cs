using System;
using System.Linq;
using System.Threading.Tasks;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Newtonsoft.Json.Linq;

namespace Arashi
{
    public class DnsJsonEncoder
    {
        public static JObject Encode(DnsMessage dnsMsg, bool randomPadding = false)
        {
            var dnsJObject = new JObject
            {
                {"Status", (int) dnsMsg.ReturnCode},
                {"TC", dnsMsg.IsTruncated},
                {"RD", dnsMsg.IsRecursionDesired},
                {"RA", dnsMsg.IsRecursionAllowed},
                {"AD", dnsMsg.IsAuthenticData},
                {"CD", dnsMsg.IsCheckingDisabled}
            };

            Parallel.Invoke(() =>
                {
                    var dnsQuestionsJArray = new JArray();
                    foreach (var dnsQjObject in dnsMsg.Questions.Select(item => new JObject
                             {
                                 {"name", item.Name.ToString()}, {"type", (int) item.RecordType}
                             })) dnsQuestionsJArray.Add(dnsQjObject);

                    dnsJObject.Add("Question", dnsQuestionsJArray);
                },
                () =>
                {
                    var dnsAnswersJArray = new JArray();
                    foreach (var item in dnsMsg.AnswerRecords)
                    {
                        var dnsAjObject = new JObject
                        {
                            {"name", item.Name.ToString()},
                            {"type", (int) item.RecordType},
                            {"TTL", item.TimeToLive}
                        };

                        switch (item)
                        {
                            case ARecord aRecord:
                                dnsAjObject.Add("data", aRecord.Address.ToString());
                                break;
                            case AaaaRecord aaaaRecord:
                                dnsAjObject.Add("data", aaaaRecord.Address.ToString());
                                break;
                            case CNameRecord cNameRecord:
                                dnsAjObject.Add("data", cNameRecord.CanonicalName.ToString());
                                break;
                            case TxtRecord txtRecord:
                                dnsAjObject.Add("data", txtRecord.TextData);
                                break;
                            default:
                            {
                                var list = item.ToString()
                                    .Split(new[] {" IN "}, StringSplitOptions.RemoveEmptyEntries)[1]
                                    .Trim().Split(' ').ToList();
                                list.RemoveAt(0);
                                dnsAjObject.Add("data", string.Join(" ", list).Replace("\"", "").Trim());
                                break;
                            }
                        }

                        dnsAjObject.Add("metadata", item.ToString());
                        dnsAnswersJArray.Add(dnsAjObject);
                    }

                    if (dnsMsg.AnswerRecords.Count > 0) dnsJObject.Add("Answer", dnsAnswersJArray);
                },
                () =>
                {
                    var authorityRecords = dnsMsg.AuthorityRecords.Where(item =>
                        !item.Name.IsSubDomainOf(DomainName.Parse("arashi-msg")) &&
                        !item.Name.IsSubDomainOf(DomainName.Parse("nova-msg")));

                    var dnsAuthorityJArray = new JArray();
                    foreach (var item in authorityRecords)
                    {
                        var dnsAujObject = new JObject
                        {
                            {"name", item.Name.ToString()},
                            {"type", (int) item.RecordType},
                            {"TTL", item.TimeToLive}
                        };

                        switch (item)
                        {
                            case ARecord aRecord:
                                dnsAujObject.Add("data", aRecord.Address.ToString());
                                break;
                            case AaaaRecord aaaaRecord:
                                dnsAujObject.Add("data", aaaaRecord.Address.ToString());
                                break;
                            case CNameRecord cNameRecord:
                                dnsAujObject.Add("data", cNameRecord.CanonicalName.ToString());
                                break;
                            default:
                            {
                                var list = item.ToString().Split(new[] {"IN"}, StringSplitOptions.RemoveEmptyEntries)[1]
                                    .Trim().Split(' ').ToList();
                                list.RemoveAt(0);
                                dnsAujObject.Add("data", string.Join(" ", list).Trim());
                                break;
                            }
                        }

                        dnsAujObject.Add("metadata", item.ToString());
                        dnsAuthorityJArray.Add(dnsAujObject);
                    }

                    if (dnsAuthorityJArray.Count > 0) dnsJObject.Add("Authority", dnsAuthorityJArray);
                },
                () =>
                {
                    var dnsNotesJArray = new JArray();
                    foreach (var item in dnsMsg.AuthorityRecords.Where(item => item.RecordType == RecordType.Txt)
                                 .ToList())
                    {
                        dnsNotesJArray.Add(new JObject
                            {{item.Name.ToString().TrimEnd('.'), ((TxtRecord) item).TextData}});
                    }

                    if (dnsNotesJArray.Count > 0) dnsJObject.Add("Notes", dnsNotesJArray);
                },
                () =>
                {
                    if (!dnsMsg.IsEDnsEnabled) return;
                    foreach (var eDnsOptionBase in dnsMsg.EDnsOptions.Options.ToArray())
                    {
                        if (eDnsOptionBase is ClientSubnetOption option)
                            dnsJObject.Add("edns_client_subnet", $"{option.Address}/{option.SourceNetmask}");
                    }
                });

            if (randomPadding)
                dnsJObject.Add("RandomPadding", Guid.NewGuid().ToString().Replace("-", "")
                    [..new Random(DateTime.Now.Millisecond).Next(1, 33)]);

            return dnsJObject;
        }
    }
}
