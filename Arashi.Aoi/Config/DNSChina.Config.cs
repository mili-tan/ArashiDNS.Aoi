using System;

namespace Arashi.Kestrel
{
    public class DNSChinaConfig
    {
        public static DNSChinaConfig Config = new DNSChinaConfig();
        public string HttpDnsEcsUrl = "http://119.29.29.29/d?dn={0}&ttl=1&ip={1}";
        public string HttpDnsUrl = "http://119.29.29.29/d?dn={0}&ttl=1";
        public string ChinaListPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "China_WhiteList.List";
        public string ChinaDnsIp = "119.29.29.29";
        public bool UseHttpDns = true;
    }
}
