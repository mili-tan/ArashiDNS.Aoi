using System;
using System.Net;

namespace Arashi.Azure
{
    public class Config
    {
        public static IPAddress UpStream = IPAddress.Parse("8.8.8.8");
        public static string QueryPerfix = "/dns-query";
        public static string AdminPerfix = "/dns-admin";
        public static string IpPerfix = "/ip";
        public static int Tries = 4;
        public static int TimeOut = 500;
        public static bool CacheEnable = true;
        public static bool LogEnable = false;
        public static bool FullLogEnable = false;
        public static bool ChinaListEnable = true;
        public static bool OnlyTcpEnable = false;
        public static bool UseIpRoute = true;
        public static bool UseAdminRoute = true;
        public static bool GeoCacheEnable = true;
        public static bool EcsEnable = true;
        public static string AdminToken = Guid.NewGuid().ToString();
    }
}
