using System;
using System.IO;
using Jering.KeyValueStore;

namespace ArashiDNS.Tools
{
    class MFaster
    {
        public static MixedStorageKVStore<string,string> FasterKv;

        public static void Init()
        {
            try
            {
                if (Directory.Exists("faster.db")) Directory.Delete("faster.db", true);
                FasterKv = new MixedStorageKVStore<string, string>(new MixedStorageKVStoreOptions()
                    {LogDirectory = "faster.db"});
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
