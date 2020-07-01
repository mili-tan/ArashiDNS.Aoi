using System;
using System.Linq;
using System.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Http;

namespace Arashi.Kestrel
{
    public class RealIP
    {
        public static string Get(HttpContext context)
        {
            try
            {
                var request = context.Request;
                if (request.Headers.ContainsKey("CF-Connecting-IP"))
                    return request.Headers["CF-Connecting-IP"].ToString();
                if (request.Headers.ContainsKey("X-Real-IP"))
                    return request.Headers["X-Real-IP"].ToString();
                if (request.Headers.ContainsKey("X-Real-IP"))
                    return request.Headers["X-Real-IP"].ToString();
                if (request.Headers.ContainsKey("X-Forwarded-For"))
                    return (request.Headers["X-Forwarded-For"].ToString().Split(',', ':').FirstOrDefault().Trim());
                return context.Connection.RemoteIpAddress.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return context.Connection.RemoteIpAddress.ToString();
            }
        }

        public static string GetFromDns(DnsMessage dnsMsg, HttpContext context)
        {
            if (!dnsMsg.IsEDnsEnabled) return Get(context);
            foreach (var eDnsOptionBase in dnsMsg.EDnsOptions.Options.ToArray())
            {
                if (eDnsOptionBase is ClientSubnetOption option)
                    return option.Address.ToString();
            }

            return Get(context);
        }

        public static bool TryGetFromDns(DnsMessage dnsMsg,out string ipAddress)
        {
            ipAddress = IPAddress.Any.ToString();
            if (!dnsMsg.IsEDnsEnabled) return false;
            foreach (var eDnsOptionBase in dnsMsg.EDnsOptions.Options.ToArray())
            {
                if (!(eDnsOptionBase is ClientSubnetOption option)) continue;
                ipAddress = option.Address.ToString();
                return true;
            }

            return false;
        }
    }
}
