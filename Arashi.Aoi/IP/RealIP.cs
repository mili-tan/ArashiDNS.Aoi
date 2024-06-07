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
                if (request.Headers.ContainsKey("Fastly-Client-IP"))
                    return IPEndPoint.Parse(request.Headers["Fastly-Client-IP"].ToString().Split(',')
                        .FirstOrDefault().Trim()).Address;
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
            catch (Exception)
            {
                try
                {
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
            try
            {
                if (dnsMsg is { IsEDnsEnabled: false }) return context.Connection.RemoteIpAddress;
                foreach (var eDnsOptionBase in dnsMsg.EDnsOptions.Options.ToList())
                    if (eDnsOptionBase is ClientSubnetOption option)
                        return option.Address;
                return context.Connection.RemoteIpAddress;

                //return (dnsMsg.EDnsOptions.Options.FirstOrDefault(i => i.Type == EDnsOptionType.ClientSubnet) as
                //    ClientSubnetOption)?.Address ?? context.Connection.RemoteIpAddress;
            }
            catch (Exception)
            {
                return context.Connection.RemoteIpAddress;
            }
        }

        public static IPAddress GetFromDns(DnsMessage dnsMsg)
        {
            try
            {
                if (dnsMsg is {IsEDnsEnabled: false}) return IPAddress.Any;
                foreach (var eDnsOptionBase in dnsMsg.EDnsOptions.Options.ToList())
                {
                    if (eDnsOptionBase is ClientSubnetOption option)
                        return option.Address;
                }

                return IPAddress.Any;
            }
            catch (Exception)
            {
                return IPAddress.Any;
            }
        }

        public static bool TryGetFromDns(DnsMessage dnsMsg, out IPAddress ipAddress)
        {
            try
            {
                ipAddress = IPAddress.Any;

                if (!dnsMsg.IsEDnsEnabled || dnsMsg.EDnsOptions.Options == null) return false;
                foreach (var eDnsOptionBase in dnsMsg.EDnsOptions.Options.ToArray())
                {
                    if (eDnsOptionBase is not ClientSubnetOption option) continue;
                    ipAddress = option.Address;
                    return !Equals(option.Address, IPAddress.Any) && !Equals(option.Address, IPAddress.Loopback);
                }

                return false;
            }
            catch (Exception)
            {
                ipAddress = IPAddress.Any;
                return false;
            }
        }
    }
}
