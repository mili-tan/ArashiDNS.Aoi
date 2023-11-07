using Microsoft.Extensions.ObjectPool;
using System.Net;
using ARSoft.Tools.Net.Dns;

namespace Arashi.Aoi
{
    public class DnsClientPooledObjectPolicy(IPAddress ip,int timeout, int port) : IPooledObjectPolicy<DnsClient>
    {

        public DnsClient Create()
        {
            return new DnsClient(ip, timeout, port != 0 ? port : 53)
                {IsUdpEnabled = !AoiConfig.Config.OnlyTcpEnable, IsTcpEnabled = true};
        }

        public bool Return(DnsClient obj)
        {
            return true;
        }
    }
}
