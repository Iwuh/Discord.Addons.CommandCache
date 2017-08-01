using System;
using System.Collections.Generic;
using System.Text;

namespace Discord.Addons.CommandCache
{
    public interface ICommandCache<TKey, TValue> : IDisposable, IEnumerable<KeyValuePair<TKey, TValue>>
    {
        IEnumerable<TKey> Keys { get; }
        IEnumerable<TValue> Values { get; }
        int Count { get; }

        TValue this[TKey key] { get; set; }

        void Add(TKey key, TValue value);
        void Clear();
        bool ContainsKey(TKey key);
        bool Remove(TKey key);
        bool TryGetValue(TKey key, out TValue value);
    }
}
