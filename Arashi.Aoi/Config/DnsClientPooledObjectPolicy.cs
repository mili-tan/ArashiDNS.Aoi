using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.ObjectPool;
using System.Net;
using ARSoft.Tools.Net.Dns;

namespace Arashi.Aoi
{
    public class DnsClientPooledObjectPolicy(IEnumerable<IPAddress> ips, int timeout, int port) : IPooledObjectPolicy<DnsClient>
    {

        public DnsClient Create() => new(ips.Where(x => !Equals(x, IPAddress.Any)),
            new IClientTransport[]
                {new UdpClientTransport(port == 0 ? 53 : port), new TcpClientTransport(port == 0 ? 53 : port)},
            false, timeout);

        public bool Return(DnsClient obj) => true;
    }
}
