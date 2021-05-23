using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Arashi
{
    public class DNSChinaConfig
    {
        public static DNSChinaConfig Config = new();
        public string ChinaListPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "China_WhiteList.List";
        public string ChinaDnsIp = "119.29.29.29";
        public bool UseHttpDns = true;

        public string HttpDnsEcsUrl = !IsIpv6Only()
            ? "http://119.29.29.29/d?dn={0}&ttl=1&ip={1}"
            : "https://dopx.netlify.app/d?dn={0}&ttl=1&ip={1}";

        public string HttpDnsUrl = !IsIpv6Only()
            ? "http://119.29.29.29/d?dn={0}&ttl=1"
            : "http://dopx.netlify.app/d?dn={0}&ttl=1";

        private static bool IsIpv6Only() =>
            Dns.GetHostAddresses(Dns.GetHostName()).All(ip =>
                IPAddress.IsLoopback(ip) || ip.AddressFamily == AddressFamily.InterNetworkV6);
    }

}
