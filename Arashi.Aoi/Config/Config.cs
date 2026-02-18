using System;

namespace Arashi
{
    public class AoiConfig
    {
        public static AoiConfig Config = new();
        public string UpStream = "8.8.8.8";
        public string BackUpStream = "9.9.9.11";
        public string QueryPerfix = "/dns-query";
        public string ReslovePerfix = "/reslove";
        public string AdminPerfix = "/dns-admin";
        public string IpPerfix = "/ip";
        public string HostName = "";
        public int TimeOut = 250;
        public int MaxTTL = 86400;
        public int MinTTL = 30;
        public int TargetTTL = 21600;
        public byte EcsDefaultMask = 24;
        public bool CacheEnable = true;
        //public bool RankEnable = false;
        public bool LogEnable = false;
        public bool FullLogEnable = false;
        public bool ChinaListEnable = false;
        public bool OnlyTcpEnable = false;
        public bool UseIpRoute = true;
        public bool UseErikoRoute = false;
        public bool UseAdminRoute = false;
        public bool UseResolveRoute = false;
        public bool UseExceptionPage = true;
        public bool UseRecursive = false;
        public bool GeoCacheEnable = true;
        public bool EcsEnable = true;
        public bool TransIdEnable = false;
        public bool TrimEndEnable = false;
        public bool RateLimitingEnable = true;
        public bool AnyTypeEnable = false;
        public bool RetellEcsEnable = true;
        public string AdminToken = Guid.NewGuid().ToString();
        public string MaxmindCityDbUrl = "https://github.com/mili-tan/maxmind-geoip/releases/latest/download/GeoLite2-City.mmdb";
        public string MaxmindAsnDbUrl = "https://github.com/mili-tan/maxmind-geoip/releases/latest/download/GeoLite2-ASN.mmdb";
    }
}
