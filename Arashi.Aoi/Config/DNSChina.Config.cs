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
        public string ChinaUpStream = "119.29.29.29";
        public string ChinaListUrl = "https://mili.one/china_whitelist.txt";
        public bool UseHttpDns = true;

        public string HttpDnsEcsUrl = !IsIpv6Only()
            ? "http://119.29.29.29/d?dn={0}&ttl=1&ip={1}"
            : BackupHttpDnsEcsUrl;

        public string HttpDnsUrl = !IsIpv6Only()
            ? "http://119.29.29.29/d?dn={0}&ttl=1"
            : BackupHttpDnsUrl;

        public static string BackupHttpDnsUrl = "http://dopx.netlify.app/d?dn={0}&ttl=1";
        public static string BackupHttpDnsEcsUrl = "https://dopx.netlify.app/d?dn={0}&ttl=1&ip={1}";

        private static bool IsIpv6Only()
        {
            return Dns.GetHostAddresses(Dns.GetHostName()).All(ip =>
                IPAddress.IsLoopback(ip) || ip.AddressFamily == AddressFamily.InterNetworkV6);
        }
    }

}
