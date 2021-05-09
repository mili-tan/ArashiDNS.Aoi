using System;

namespace Arashi
{
    public class AoiConfig
    {
        public static AoiConfig Config = new();
        public string UpStream = "8.8.8.8";
        public string BackUpStream = "8.8.4.4";
        public string QueryPerfix = "/dns-query";
        public string AdminPerfix = "/dns-admin";
        public string IpPerfix = "/ip";
        public int Retries = 4;
        public int TimeOut = 250;
        public byte EcsDefaultMask = 24;
        public bool CacheEnable = true;
        public bool RankEnable = false;
        public bool LogEnable = false;
        public bool FullLogEnable = false;
        public bool ChinaListEnable = false;
        public bool OnlyTcpEnable = false;
        public bool UseIpRoute = true;
        public bool UseAdminRoute = true;
        public bool UseExceptionPage = true;
        public bool GeoCacheEnable = true;
        public bool EcsEnable = true;
        public string AdminToken = Guid.NewGuid().ToString();
        public string MaxmindCityDbUrl = "https://t.mili.one/GeoLite2-City.mmdb";
        public string MaxmindAsnDbUrl = "https://t.mili.one/GeoLite2-ASN.mmdb";
    }
}
