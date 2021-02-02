using System.Linq;
using ARSoft.Tools.Net;
using LiteDB;

namespace Arashi.Aoi.DNS
{
    class DNSRank
    {
        private static LiteDatabase database = new(@"rank.db");
        private static ILiteCollection<Rank> collection = database.GetCollection<Rank>("FullRank");
        private class Rank
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Count { get; set; }
        }

        public static void AddUp(DomainName name)
        {
            var find = collection.Find(x => x.Name == name.ToString()).ToList();
            if (find.Any())
            {
                find.FirstOrDefault().Count += 1;
                collection.Update(find.FirstOrDefault());
            }
            else
            {
                collection.Insert(new Rank {Name = name.ToString(), Count = 1});
            }
        }
    }
}
