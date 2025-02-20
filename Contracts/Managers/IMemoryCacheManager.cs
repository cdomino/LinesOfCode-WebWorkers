using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LinesOfCode.Web.Workers.Contracts.Managers
{
    public interface IMemoryCacheManager<K, V>
    {
        #region Methods
        void Clear();
        void Delete(K key);
        Task<V> GetAsync(K key);
        IEnumerable<K> GetAllKeys();
        void AddOrUpdate(K key, V value);
        V GetOrAdd(K key, Func<K, V> valueFactory);
        Task<V> GetOrAddAsync(K key, Func<K, Task<V>> valueFactoryAsync);
        #endregion
    }
}
