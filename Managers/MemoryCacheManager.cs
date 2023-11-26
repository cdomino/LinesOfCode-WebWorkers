using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using LinesOfCode.Web.Workers.Contracts;
using LinesOfCode.Web.Workers.Utilities;

namespace LinesOfCode.Web.Workers.Managers
{
    /// <summary>
    /// This wraps the usage of static dictionaries for use in low fidelity memory cache scenarios. Declare instances of this statically, and only assign from DI in consumer constructors when null.
    /// </summary>
    public class MemoryCacheManager<K, V> : IMemoryCacheManager<K, V>
    {
        #region Members
        private readonly ILogger<MemoryCacheManager<K, V>> _logger;
        private static readonly ConcurrentDictionary<K, Lazy<V>> _syncDictionary = new ConcurrentDictionary<K, Lazy<V>>();
        private static readonly ConcurrentDictionary<K, Lazy<Task<V>>> _asyncDictionary = new ConcurrentDictionary<K, Lazy<Task<V>>>();
        #endregion
        #region Initialization
        public MemoryCacheManager(ILogger<MemoryCacheManager<K, V>> logger)
        {
            //initialization
            this._logger = logger;
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Uses lazy to ensure every key is only created once async.
        /// </summary>
        public async Task<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactoryAsync)
        {
            //initialization
            this._logger.LogTrace($"Getting or adding key {key} async.");
            Lazy<Task<V>> result = MemoryCacheManager<K, V>._asyncDictionary.GetOrAdd(key, new Lazy<Task<V>>(async () => await valueFactoryAsync(key)));

            //return
            return await result.Value;
        }

        /// <summary>
        /// Uses lazy to ensure every key is only created once.
        /// </summary>
        public V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            //initialization
            this._logger.LogTrace($"Getting or adding key {key}.");
            Lazy<V> result = MemoryCacheManager<K, V>._syncDictionary.GetOrAdd(key, new Lazy<V>(() => valueFactory(key)));

            //return
            return result.Value;
        }

        /// <summary>
        /// Updates an existing entry.
        /// </summary>
        public void AddOrUpdate(K key, V value)
        {
            //initialization
            this._logger.LogTrace($"Adding or updating key {key}.");

            //return
            MemoryCacheManager<K, V>._syncDictionary.Add(key, new Lazy<V>(() => value));
        }

        /// <summary>
        /// Gets all the cache keys.
        /// </summary>
        public IEnumerable<K> GetAllKeys()
        {
            //initialization
            List<K> keys = MemoryCacheManager<K, V>._asyncDictionary.Keys.ToList();

            //combine all keys
            keys.AddRange(MemoryCacheManager<K, V>._syncDictionary.Keys);
            keys = keys.Distinct().ToList();

            //return
            this._logger.LogTrace($"Got {keys.Pluralize("key")} of {typeof(K).FullName}.");
            return keys;
        }

        /// <summary>
        /// Clears all the cache keys.
        /// </summary>
        public void Clear()
        {
            //initialization
            MemoryCacheManager<K, V>._syncDictionary.Clear();
            MemoryCacheManager<K, V>._asyncDictionary.Clear();

            //return
            this._logger.LogTrace("Cleared cache keys.");
        }

        /// <summary>
        /// Gets an item by key.
        /// </summary>
        public async Task<V> GetAsync(K key)
        {
            //initialization
            this._logger.LogTrace($"Getting {key}.");

            //return
            if (MemoryCacheManager<K, V>._asyncDictionary.ContainsKey(key))
                return await MemoryCacheManager<K, V>._asyncDictionary[key].Value;
            else if (MemoryCacheManager<K, V>._syncDictionary.ContainsKey(key))
                return await Task.FromResult<V>(MemoryCacheManager<K, V>._syncDictionary[key].Value);
            else
                return default(V);
        }

        /// <summary>
        /// Clears all the cache keys.
        /// </summary>
        public void Delete(K key)
        {
            //initialization
            bool result = MemoryCacheManager<K, V>._asyncDictionary.Remove(key, out _);

            //return
            if (result)
            {
                //found async
                this._logger.LogTrace($"Deleted async cache key {key} of type {typeof(K).FullName}.");
            }
            else
            {
                //check sync
                result = MemoryCacheManager<K, V>._syncDictionary.Remove(key, out _);
                if (result)
                    this._logger.LogTrace($"Deleted sync cache key {key} of type {typeof(K).FullName}.");
                else
                    this._logger.LogWarning($"Unable to delete cache key {key} of type {typeof(K).FullName}.");
            }
        }
        #endregion  
    }
}
