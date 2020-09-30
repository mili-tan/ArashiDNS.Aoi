using System;
using System.Linq;
using System.Threading.Tasks;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Newtonsoft.Json.Linq;

namespace Arashi
{
    public class DohJsonEncoder
    {
        public static JObject Encode(DnsMessage dnsMsg)
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

            var tQuestion = Task.Run(() =>
            {
                var dnsQuestionsJArray = new JArray();
                foreach (var dnsQjObject in dnsMsg.Questions.Select(item => new JObject
                {
                    {"name", item.Name.ToString()}, {"type", (int) item.RecordType}
                })) dnsQuestionsJArray.Add(dnsQjObject);

                dnsJObject.Add("Question", dnsQuestionsJArray);
            });

            var tAnswer = Task.Run(() =>
            {
                var dnsAnswersJArray = new JArray();
                var dnsNotesJArray = new JArray();
                foreach (var item in dnsMsg.AnswerRecords)
                {
                    if (item.Name.IsSubDomainOf(DomainName.Parse("arashi-msg")) && item.RecordType == RecordType.Txt)
                    {
                        dnsNotesJArray.Add(new JObject
                            {{item.Name.ToString().TrimEnd('.'), ((TxtRecord) item).TextData}});
                        continue;
                    }

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
                        default:
                        {
                            var list = item.ToString()
                                .Split(new[] {"IN"}, StringSplitOptions.RemoveEmptyEntries)[1]
                                .Trim().Split(' ').ToList();
                            list.RemoveAt(0);
                            dnsAjObject.Add("data", string.Join(" ", list).Trim());
                            break;
                        }
                    }

                    dnsAjObject.Add("metadata", item.ToString());
                    dnsAnswersJArray.Add(dnsAjObject);
                }

                if (dnsMsg.AnswerRecords.Count > 0) dnsJObject.Add("Answer", dnsAnswersJArray);
                if (dnsNotesJArray.Count > 0) dnsJObject.Add("Notes", dnsNotesJArray);
            });

            var tAuthority = Task.Run(() =>
            {
                var dnsAuthorityJArray = new JArray();
                foreach (var item in dnsMsg.AuthorityRecords)
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

                if (dnsMsg.AuthorityRecords.Count > 0) dnsJObject.Add("Authority", dnsAuthorityJArray);
            });

            
            var tEDns = Task.Run(() =>
            {
                if (!dnsMsg.IsEDnsEnabled) return;
                foreach (var eDnsOptionBase in dnsMsg.EDnsOptions.Options.ToArray())
                {
                    if (eDnsOptionBase is ClientSubnetOption option)
                        Task.Run(() =>
                        {
                            Task.WaitAll(tAnswer, tAuthority);
                            dnsJObject.Add("edns_client_subnet", $"{option.Address}/{option.SourceNetmask}");
                        });
                }
            });

            Task.WaitAll(tQuestion, tAnswer, tAuthority, tEDns);
            return dnsJObject;
        }
    }
}
