using Microsoft.Extensions.ObjectPool;
using System.Net;
using ARSoft.Tools.Net.Dns;

namespace Arashi.Aoi
{
    public class DnsClientPooledObjectPolicy(IPAddress ip,int timeout, int port) : IPooledObjectPolicy<DnsClient>
    {

        public DnsClient Create()
        {
            return new DnsClient(ip, timeout);
        }

        public bool Return(DnsClient obj)
        {
            return true;
        }
    }
}
