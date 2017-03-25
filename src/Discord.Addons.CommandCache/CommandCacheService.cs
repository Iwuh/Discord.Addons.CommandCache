using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Linq;

namespace Discord.Addons.CommandCache
{
    public class CommandCacheService : IDictionary<ulong, ulong>, IDisposable
    {
        public const int UNLIMITED = -1; // POWEEEEEEERRRRRRRR

        private List<KeyValuePair<ulong, ulong>> _cache;
        private int _max;
        private Timer _autoClear;
        private Func<LogMessage, Task> _logger;

        /// <summary>
        /// Initialises the cache with a maximum capacity, tracking the client's message deleted event.
        /// </summary>
        /// <param name="client">The client that the MessageDeleted handler should be hooked up to.</param>
        /// <param name="capacity">The maximum capacity of the cache. A value of CommandCacheService.UNLIMITED signifies no maximum capacity.</param>
        /// <param name="log">An optional method to use for logging.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if capacity is less than 1 and is not -1 (unlimited).</exception>
        public CommandCacheService(DiscordSocketClient client, int capacity = 200, Func<LogMessage, Task> log = null)
        {
            // If a method for logging is supplied, use it, otherwise use a method that does nothing.
            _logger = log ?? (_ => Task.CompletedTask);

            // Make sure the max capacity is within an acceptable range, use it if it is.
            if (capacity < 1 && capacity != UNLIMITED)
            {
                throw new ArgumentOutOfRangeException("Capacity can not be lower than 1 unless capacity is CommandCacheService.UNLIMITED.");
            }
            else
            {
                _max = capacity;
            }

            // TODO: Add message deleted & timer handlers.
        }

        ~CommandCacheService()
        {
            if (_autoClear != null)
            {
                _autoClear.Dispose();
                _autoClear = null;
            }
        }

        /// <summary>
        /// Gets all the keys in the cache.
        /// </summary>
        public ICollection<ulong> Keys => _cache.Select(p => p.Key).ToList();

        /// <summary>
        /// Gets all the values in the cache.
        /// </summary>
        public ICollection<ulong> Values => _cache.Select(p => p.Value).ToList();

        /// <summary>
        /// Gets the number of command/response pairs in the cache.
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// Always returns false. Why would you ever need this to be read-only?
        /// </summary>
        public bool IsReadOnly => false;

        public ulong this[ulong key]
        {
            // TODO: Implement indexer.
        }

        public void Add(ulong key, ulong value)
        {
            throw new NotImplementedException();
        }

        public void Add(KeyValuePair<ulong, ulong> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(KeyValuePair<ulong, ulong> item)
        {
            throw new NotImplementedException();
        }

        public bool ContainsKey(ulong key)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(KeyValuePair<ulong, ulong>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<ulong, ulong>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public bool Remove(ulong key)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<ulong, ulong> item)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(ulong key, out ulong value)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
