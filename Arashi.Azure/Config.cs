﻿using System.Net;

namespace Arashi.Azure
{
    public class Config
    {
        public static IPAddress UpStream = IPAddress.Parse("8.8.8.8");
        public static string QueryPerfix = "/dns-query";
        public static string CachePerfix = "/cache";
        public static string IpPerfix = "/ip";
        public static int Tries = 4;
        public static int TimeOut = 500;
    }
}