using System;
using System.Net.Sockets;

namespace Arashi.Kestrel
{
    public class DNSChinaConfig
    {
        public static DNSChinaConfig Config = new DNSChinaConfig();
        public string ChinaListPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "China_WhiteList.List";
        public string ChinaDnsIp = "119.29.29.29";
        public bool UseHttpDns = true;

        public string HttpDnsEcsUrl = Socket.OSSupportsIPv4
            ? "http://119.29.29.29/d?dn={0}&ttl=1&ip={1}"
            : "https://dopx.netlify.app/d?dn={0}&ttl=1&ip={1}";

        public string HttpDnsUrl = Socket.OSSupportsIPv4
            ? "http://119.29.29.29/d?dn={0}&ttl=1"
            : "http://dopx.netlify.app/d?dn={0}&ttl=1";
    }
}
