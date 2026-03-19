using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PrviZadatak.Configuration;
using PrviZadatak.Logging;

namespace PrviZadatak.CacheManager
{
    public class Cache
    {
        private readonly AppSettings _appSettings;

        private static Dictionary<string, int> _cache = new Dictionary<string, int>();
        private static LinkedList<string> accessOrder = new LinkedList<string>();

        private readonly object _lock = new object();

        public Cache(AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        public bool TryGet(string key, out int value)
        {
            lock(_lock)
            {
                bool isInCache = _cache.TryGetValue(key, out value);

                if (isInCache)
                {
                    accessOrder.Remove(key);
                    accessOrder.AddLast(key);
                }
                return isInCache;
            }
        }

        public void Add(string key, int value)
        {
            lock(_lock)
            {
                if(_cache.Count >= _appSettings.CacheSize)
                {
                    Remove();
                }
                if(_cache.TryAdd(key, value))
                {
                    accessOrder.AddLast(key);
                }
            }
        }
        private void Remove()
        {
            try
            {
                if (accessOrder.First == null)
                    return;

                Logger.Log($"Uklanjanje stavke iz keša: {accessOrder.First.Value}");
                _cache.Remove(accessOrder.First.Value);
                accessOrder.RemoveFirst();
            }
            catch (Exception ex)
            {
                Logger.Log($"Greska prilikom uklanjanja stavke iz keša: {ex.Message}");
            }
        }
    }
}
