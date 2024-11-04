using Microsoft.Extensions.Caching;
using System.Runtime.Caching;
using System.Text.Json;

namespace MinimalAPI.Extensions
{
    public static class SessionExtensions
    {
        private static ObjectCache _Cache;
        private const string SESSION_CACHE_KEY = "$sessioncache";

        static SessionExtensions()
        {
            _Cache = new MemoryCache("sessionCache");
        }

        private static Dictionary<string, object> GetPrivateObjectStore(string cacheKey)
        {
            CacheItem ci = _Cache.GetCacheItem(cacheKey);

            if (ci != null)
            {
                return (Dictionary<string, object>)ci.Value;
            }

            return null;
        }

        public static void InitObjectStore(this ISession session)
        {
            Dictionary<string, object> sessionObjects = new Dictionary<string, object>();
            CacheItemPolicy policy = new CacheItemPolicy();

            //set sliding timer to session time + 1, so that cache do not expire before session does
            policy.SlidingExpiration = TimeSpan.FromMinutes(120 + 1);

            CacheItem ci = new CacheItem(session.Id + SESSION_CACHE_KEY);
            ci.Value = sessionObjects;

            _Cache.Set(ci, policy);
        }

        public static void RemoveObjectStore(this ISession session)
        {
            string cacheKey = session.Id + SESSION_CACHE_KEY;

            Dictionary<string, object> objectStore = GetPrivateObjectStore(cacheKey);

            objectStore.Clear();

            //also remove the collection from cache
            _Cache.Remove(cacheKey);
        }

        public static Dictionary<string, object> GetAllObjectsKeyValuePair(this ISession session)
        {
            Dictionary<string, object> objectStore = GetPrivateObjectStore(session.Id + SESSION_CACHE_KEY);

            return objectStore;
        }

        public static void RemoveObject(this ISession session, string key)
        {
            Dictionary<string, object> objectStore = GetPrivateObjectStore(session.Id + SESSION_CACHE_KEY);

            if (objectStore != null)
            {
                objectStore.Remove(key);
            }
        }

        public static void SetObject(this ISession session, string key, object value)
        {
            Dictionary<string, object> objectStore = GetPrivateObjectStore(session.Id + SESSION_CACHE_KEY);

            if (objectStore == null)
            {
                //ensure object store is available so that existing legacy code doesn't break
                InitObjectStore(session);
            }

            if (value == null)
            {
                objectStore.Remove(key);
            }
            else
            {
                objectStore[key] = value;
            }
        }

        public static object GetObject(this ISession session, string key)
        {
            Dictionary<string, object> objectStore = GetPrivateObjectStore(session.Id + SESSION_CACHE_KEY);

            object result = null;

            if (objectStore != null)
            {
                result = objectStore[key];
            }

            return result;
        }
    }
}
