using Microsoft.Extensions.ObjectPool;
using System.Net;
using ARSoft.Tools.Net.Dns;

namespace Arashi.Aoi
{
    public class DnsClientPooledObjectPolicy(IPAddress ip,int timeout,int port) : IPooledObjectPolicy<DnsClient>
    {

        public DnsClient Create() => new(new[] {ip},
            new IClientTransport[]
                {new UdpClientTransport(port == 0 ? 53 : port), new TcpClientTransport(port == 0 ? 53 : port)},
            false, timeout);

        public bool Return(DnsClient obj) => true;
    }
}
