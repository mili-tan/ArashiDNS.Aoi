using System.Reflection;
using System.Threading.Tasks;
using ARSoft.Tools.Net.Dns;

namespace Arashi
{
    public static class DnsEncoder
    {
        private static MethodInfo info;

        public static void Init()
        {
            Parallel.ForEach(new DnsMessage().GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic), mInfo =>
            {
                if (mInfo.ToString() == "Int32 Encode(Boolean, Byte[] ByRef)")
                    info = mInfo;
            });
        }

        public static byte[] Encode(DnsMessage dnsQMsg)
        {
            if (info == null) Init();
            var args = new object[] {false, null};
            if (info != null) info.Invoke(dnsQMsg, args);
            var dnsBytes = args[1] as byte[];
            if (dnsBytes != null && dnsBytes[2] == 0) dnsBytes[2] = 1;
            return dnsBytes;
        }
    }
}
