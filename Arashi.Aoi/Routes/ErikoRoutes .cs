using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;

namespace Arashi.Aoi.Routes
{
    class ErikoRoutes
    {
        public static void ErikoRoute(IEndpointRouteBuilder endpoints)
        {

            endpoints.Map("/eriko/ping/icmp/{addr}", async context =>
            {
                var res = ping(context.GetRouteValue("addr").ToString(), 1);
                var obj = new
                {
                    protocol = "tcp",
                    state = (res.times.Sum() != 0),
                    latency = res.times.Average(),
                    msg = (res.times.Sum() != 0) ? "OK" : "Timeout",
                    res.ttl,
                    ip = res.ip.ToString()
                };
                await context.WriteResponseAsync(JsonConvert.SerializeObject(obj, Formatting.Indented),
                    type: "application/json");
            });
            endpoints.Map("/eriko/ping/tcp/{addr}/{port}", async context =>
            {
                var res = tcping(context.GetRouteValue("addr").ToString(),
                    int.Parse(context.GetRouteValue("port").ToString()), 1);
                var obj = new
                {
                    protocol = "tcp",
                    state = (res.times.Sum() != 0),
                    latency = res.times.Average(),
                    msg = (res.times.Sum() != 0) ? "OK" : "Timeout",
                    res.ttl,
                    ip = res.ip.ToString()
                };
                await context.WriteResponseAsync(JsonConvert.SerializeObject(obj, Formatting.Indented), type: "application/json");
            });
            endpoints.Map("/eriko/dns/query/{addr}", async context =>
            {
                try
                {
                    var res = await new DnsClient(IPAddress.Parse("8.8.8.8"), 1000).ResolveAsync(
                        DomainName.Parse(context.GetRouteValue("addr").ToString()));
                    var obj = new
                    {
                        protocol = "dns",
                        state = true,
                        msg = "OK",
                        rcode = res.ReturnCode,
                        ttl = res.AnswerRecords.First().TimeToLive,
                        rtype = res.AnswerRecords.First().RecordType,
                        rdata = res.AnswerRecords.First().ToString().Split(' ').Last()
                    };
                    await context.WriteResponseAsync(JsonConvert.SerializeObject(obj, Formatting.Indented),
                        type: "application/json");
                }
                catch (Exception)
                {
                    await context.WriteResponseAsync(JsonConvert.SerializeObject(new
                    {
                        protocol = "dns",
                        state = false,
                        msg = "Error"
                    }, Formatting.Indented), type: "application/json");
                }
            });

            endpoints.Map("/eriko/dns/check/{addr}", async context =>
            {
                try
                {
                    var res = await new DnsClient(IPAddress.Parse("93.184.216.34"), 100).ResolveAsync(
                        DomainName.Parse(context.GetRouteValue("addr").ToString()));
                    if (res.AnswerRecords.Count == 0) throw new Exception();
                    var obj = new
                    {
                        protocol = "dns",
                        state = true,
                        spoof = true,
                        msg = "probably",
                        rcode = res.ReturnCode,
                        ttl = res.AnswerRecords.First().TimeToLive,
                        rtype = res.AnswerRecords.First().RecordType,
                        rdata = res.AnswerRecords.First().ToString().Split(' ').Last()
                    };
                    await context.WriteResponseAsync(JsonConvert.SerializeObject(obj, Formatting.Indented),
                        type: "application/json");
                }
                catch (Exception)
                {
                    await context.WriteResponseAsync(JsonConvert.SerializeObject(new
                    {
                        protocol = "dns",
                        state = true,
                        spoof = false,
                        msg = "probably not"
                    }, Formatting.Indented), type: "application/json");
                }
            });
        }

        private static (List<int> times,int ttl,IPAddress ip) tcping(string addr, int port, int len)
        {
            var times = new List<int>();
            var ttl = 0;
            var ip = IPAddress.Any;
            for (var i = 0; i < len; i++)
            {
                var socks = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    { Blocking = true, ReceiveTimeout = 1000, SendTimeout = 1000 };
                IPEndPoint point;
                try
                {
                    point = new IPEndPoint(IPAddress.Parse(addr), port);
                }
                catch
                {
                    point = new IPEndPoint(Dns.GetHostAddresses(addr)[0], port);
                }
                var stopWatch = new Stopwatch();
                stopWatch.Start();
                try
                {
                    var result = socks.BeginConnect(point, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(1000, true)) continue;
                    ttl = socks.Ttl;
                    ip = point.Address;
                }
                catch
                {
                    continue;
                }
                stopWatch.Stop();
                times.Add(Convert.ToInt32(stopWatch.Elapsed.TotalMilliseconds));
                socks.Close();
                Thread.Sleep(50);
            }

            if (times.Count == 0) times.Add(0);
            return (times,ttl,ip);
        }

        private static (List<int> times, int ttl, IPAddress ip) ping(string addr, int len)
        {
            var ping = new Ping();
            var ttl = 0;
            var ip = IPAddress.Any;
            var bufferBytes = Encoding.Default.GetBytes("abcdefghijklmnopqrstuvwabcdefghi");

            var times = new List<int>();
            for (var i = 0; i < len; i++)
            {
                var res = ping.Send(addr, 1000, bufferBytes);
                if (res != null)
                {
                    times.Add(Convert.ToInt32(res.RoundtripTime));
                    ttl = res.Options.Ttl;
                    ip = res.Address;
                }

                Thread.Sleep(50);
            }

            if (times.Count == 0) times.Add(0);
            return (times, ttl, ip);
        }
    }
}
