using System;
using System.Linq;
using System.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Http;

namespace Arashi
{
    public class RealIP
    {
        public static IPAddress Get(HttpContext context)
        {
            try
            {
                var request = context.Request;
                if (request.Headers.ContainsKey("X-Forwarded-For"))
                    return IPEndPoint.Parse(request.Headers["X-Forwarded-For"].ToString().Split(',')
                        .FirstOrDefault().Trim()).Address;
                if (request.Headers.ContainsKey("X-Vercel-Forwarded-For"))
                    return IPEndPoint.Parse(request.Headers["X-Vercel-Forwarded-For"].ToString().Split(',')
                        .FirstOrDefault().Trim()).Address;
                if (request.Headers.ContainsKey("CF-Connecting-IP"))
                    return IPEndPoint.Parse(request.Headers["CF-Connecting-IP"].ToString().Split(',')
                        .FirstOrDefault().Trim()).Address;
                if (request.Headers.ContainsKey("X-Real-IP"))
                    return IPEndPoint.Parse(request.Headers["X-Real-IP"].ToString().Split(',').FirstOrDefault().Trim())
                        .Address;
                return context.Connection.RemoteIpAddress;
            }
            catch (Exception e)
            {
                try
                {
                    Console.WriteLine(e);
                    return context.Connection.RemoteIpAddress;
                }
                catch (Exception)
                {
                    return IPAddress.Any;
                }
            }
        }

        public static IPAddress GetFromDns(DnsMessage dnsMsg, HttpContext context)
        {
            if (!dnsMsg.IsEDnsEnabled) return Get(context);
            foreach (var eDnsOptionBase in dnsMsg.EDnsOptions.Options.ToArray())
            {
                if (eDnsOptionBase is ClientSubnetOption option)
                    return option.Address;
            }

            return Get(context);
        }

        public static bool TryGetFromDns(DnsMessage dnsMsg, out IPAddress ipAddress)
        {
            ipAddress = IPAddress.Any;

            if (!dnsMsg.IsEDnsEnabled) return false;
            foreach (var eDnsOptionBase in dnsMsg.EDnsOptions.Options.ToArray())
            {
                if (!(eDnsOptionBase is ClientSubnetOption option)) continue;
                ipAddress = option.Address;
                return !Equals(option.Address, IPAddress.Any) && !Equals(option.Address, IPAddress.Loopback);
            }

            return false;
        }
    }
}
