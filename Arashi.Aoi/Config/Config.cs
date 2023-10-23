using System;

namespace Arashi
{
    public class AoiConfig
    {
        public static AoiConfig Config = new();
        public string UpStream = "8.8.8.8";
        public string BackUpStream = "1.0.0.1";
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
        public bool UseErikoRoute = false;
        public bool UseAdminRoute = false;
        public bool UseResolveRoute = false;
        public bool UseExceptionPage = true;
        public bool GeoCacheEnable = true;
        public bool EcsEnable = true;
        public bool TransIdEnable = false;
        public bool RateLimitingEnable = true;
        public bool AnyTypeEnable = false;
        public string AdminToken = Guid.NewGuid().ToString();
        public string MaxmindCityDbUrl = "https://gh.mili.one/github.com/mili-tan/maxmind-geoip/releases/latest/download/GeoLite2-City.mmdb";
        public string MaxmindAsnDbUrl = "https://gh.mili.one/github.com/mili-tan/maxmind-geoip/releases/latest/download/GeoLite2-ASN.mmdb";
    }
}
