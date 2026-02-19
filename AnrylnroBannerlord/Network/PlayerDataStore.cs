using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnrylnroBannerlord.Network
{
    public static class PlayerDataStore
    {
        private static readonly object _lock = new object();
        private static List<PlayerSnapshot> _cache = new List<PlayerSnapshot>();

        public static volatile bool RefreshRequested = false;

        public static void RequestRefresh()
        {
            RefreshRequested = true;
        }

        public static void Update(List<PlayerSnapshot> data)
        {
            lock (_lock)
            {
                _cache = data;
            }
        }

        public static List<PlayerSnapshot> GetSnapshot()
        {
            lock (_lock)
            {
                return new List<PlayerSnapshot>(_cache);
            }
        }
    }
}
