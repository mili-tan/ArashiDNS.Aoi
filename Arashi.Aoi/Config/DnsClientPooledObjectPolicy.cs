using Microsoft.Extensions.ObjectPool;
using System.Net;
using ARSoft.Tools.Net.Dns;

namespace Arashi.Aoi
{
    public class DnsClientPooledObjectPolicy(IPAddress ip,int timeout) : IPooledObjectPolicy<DnsClient>
    {

        public DnsClient Create() => new(ip, timeout);

        public bool Return(DnsClient obj) => true;
    }
}
