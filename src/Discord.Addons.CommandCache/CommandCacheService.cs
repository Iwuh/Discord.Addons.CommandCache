using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Linq;
using Discord.Net;
using System.Collections.Concurrent;

namespace Discord.Addons.CommandCache
{
    /// <summary>
    /// A thread-safe class used to automatically delete response messages when the command message is deleted.
    /// </summary>
    public class CommandCacheService : IDictionary<ulong, ConcurrentBag<ulong>>, IDisposable
    {
        public const int UNLIMITED = -1; // POWEEEEEEERRRRRRRR

        private ConcurrentDictionary<ulong, ConcurrentBag<ulong>> _cache;
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
            // Initialise the cache.
            _cache = new ConcurrentDictionary<ulong, ConcurrentBag<ulong>>();

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

            _autoClear = new Timer(_ =>
            {
                /*
                 * Get all messages where the timestamp is older than 2 hours. Then convert it to a list. The reason for this is that
                 * Where is lazy, and the elements of the IEnumerable are merely references to the elements of the original collection.
                 * So, iterating over the query result and removing each element from the original collection will throw an exception.
                 * By using ToList, the elements are copied over to a new collection, and thus will not throw an exception.
                 */
                var purge = _cache.Where(p =>
                {
                    // The timestamp of a message can be calculated by getting the leftmost 42 bits of the ID, then
                    // adding January 1, 2015 as a Unix timestamp.
                    DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)((p.Key >> 22) + 1420070400000UL));
                    TimeSpan difference = DateTimeOffset.UtcNow - timestamp;

                    return difference.TotalHours >= 2.0;
                }).ToList();

                var removed = purge.Select(p => Remove(p));

                _logger(new LogMessage(LogSeverity.Verbose, "Command Cache", $"Cleaned {removed.Count()} items from the cache."));
            }, null, 7200000, 7200000); // 7,200,000 ms = 2 hrs

            client.MessageDeleted += async (cacheable, channel) =>
            {
                if (ContainsKey(cacheable.Id))
                {
                    var messages = _cache[cacheable.Id];

                    foreach (var messageId in messages)
                    {
                        try
                        {
                            var message = await channel.GetMessageAsync(messageId);
                            await message.DeleteAsync();
                        }
                        catch (NullReferenceException)
                        {
                            await _logger(new LogMessage(LogSeverity.Warning, "Command Cache", $"{cacheable.Id} deleted but {this[cacheable.Id]} does not exist."));
                        }
                        finally
                        {
                            Remove(cacheable.Id);
                        }
                    }
                }
            };

            _logger(new LogMessage(LogSeverity.Verbose, "Command Cache", $"Service initialised, MessageDeleted successfully hooked."));
        }

        ~CommandCacheService()
        {
            Dispose();
        }

        /// <summary>
        /// Gets all the keys in the cache. Will claim all locks until the operation is complete.
        /// </summary>
        public ICollection<ulong> Keys => _cache.Keys;

        /// <summary>
        /// Gets all the values in the cache. Will claim all locks until the operation is complete.
        /// </summary>
        public ICollection<ConcurrentBag<ulong>> Values => _cache.Values;

        /// <summary>
        /// Gets the number of command/response sets in the cache. Will claim all locks until the operation is complete.
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// Gets or sets whether or not the cache is read-only.
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the value of a set by using the key.
        /// </summary>
        /// <param name="key">The key to search with.</param>
        /// <exception cref="KeyNotFoundException">Thrown if key is not in the cache.</exception>
        public ConcurrentBag<ulong> this[ulong key]
        {
            get
            {
                return _cache[key];
            }

            set
            {
                if (IsReadOnly) throw new InvalidOperationException("The command cache is set to read only.");

                _cache[key] = value;
            }
        }

        /// <summary>
        /// Adds a key and multiple values to the cache, or extends the existing values if the key already exists.
        /// </summary>
        /// <param name="key">The id of the command message.</param>
        /// <param name="values">The ids of the response messages.</param>
        public void Add(ulong key, ConcurrentBag<ulong> values)
        {
            if (IsReadOnly) throw new InvalidOperationException("The command cache is set to read only.");

            if (_max != UNLIMITED && _cache.Count >= _max)
            {
                int removeCount = (_cache.Count - _max) + 1;
                // The left 42 bits represent the timestamp.
                var orderedKeys = _cache.Keys.OrderBy(k => k >> 22).ToList();
                for (int i = 0; i < removeCount; i++)
                {
                    Remove(orderedKeys[i]);
                }
            }
            _cache.AddOrUpdate(key, values, ((existingKey, existingValues) => existingValues.AddMany(values)));
        }

        /// <summary>
        /// Adds a new set to the cache, or extends the existing values if the key already exists.
        /// </summary>
        /// <param name="pair">The key, and its values.</param>
        public void Add(KeyValuePair<ulong, ConcurrentBag<ulong>> pair) => Add(pair.Key, pair.Value);

        /// <summary>
        /// Adds a new key and value to the cache, or extends the values of an existing key.
        /// </summary>
        /// <param name="key">The id of the command message.</param>
        /// <param name="value">The id of the response message.</param>
        public void Add(ulong key, ulong value)
        {
            if (IsReadOnly) throw new InvalidOperationException("The command cache is set to read only.");

            if (ContainsKey(key))
            {
                _cache[key].Add(value);
            }
            else
            {
                Add(key, new ConcurrentBag<ulong>() { value });
            }
        }

        /// <summary>
        /// Adds a key and multiple values to the cache, or extends the existing values if the key already exists.
        /// </summary>
        /// <param name="key">The id of the command message.</param>
        /// <param name="values">The ids of the response messages.</param>
        public void Add(ulong key, params ulong[] values) => Add(key, new ConcurrentBag<ulong>(values));

        /// <summary>
        /// Clears all items from the cache. Will claim all locks until the operation is complete.
        /// </summary>
        public void Clear() => _cache.Clear();

        /// <summary>
        /// Checks whether the cache contains a set with a certain key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>Whether or not the key was found.</returns>
        public bool ContainsKey(ulong key) => _cache.ContainsKey(key);

        /// <summary>
        /// Checks whether the cache contains a key, and whether the key has the specified values.
        /// </summary>
        /// <param name="pair">The key and values to search for.</param>
        /// <returns>Whether or not the key was found with identical values.</returns>
        bool ICollection<KeyValuePair<ulong, ConcurrentBag<ulong>>>.Contains(KeyValuePair<ulong, ConcurrentBag<ulong>> pair)
        {
            if (TryGetValue(pair.Key, out ConcurrentBag<ulong> values))
            {
                return values.SequenceEqual(pair.Value);
            }
            return false;
        }

        /// <summary>
        /// Copies a range of the cache to an array, starting at a specified index and going until the last element. Will claim all locks until the operation is complete.
        /// </summary>
        /// <param name="array">The array to copy to.</param>
        /// <param name="arrayIndex">The index to start copying from.</param>
        void ICollection<KeyValuePair<ulong, ConcurrentBag<ulong>>>.CopyTo(KeyValuePair<ulong, ConcurrentBag<ulong>>[] array, int arrayIndex) => ((IDictionary)_cache).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<ulong, ConcurrentBag<ulong>>> GetEnumerator() => _cache.GetEnumerator();

        /// <summary>
        /// Removes a set from the cache by key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>Whether or not the removal operation was successful.</returns>
        public bool Remove(ulong key) => _cache.TryRemove(key, out ConcurrentBag<ulong> _);

        /// <summary>
        /// Removes a set from the cache.
        /// </summary>
        /// <param name="item">The command/response set to remove.</param>
        /// <returns>Whether or not the removal operation was successful.</returns>
        public bool Remove(KeyValuePair<ulong, ConcurrentBag<ulong>> item) => Remove(item.Key);

        /// <summary>
        /// Tries to get the values of a set by key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <param name="value">The values of the set (0 if the key is not found).</param>
        /// <returns>Whether or not key was found in the cache.</returns>
        public bool TryGetValue(ulong key, out ConcurrentBag<ulong> value) => _cache.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Safely disposes of the private timer.
        /// </summary>
        public void Dispose()
        {
            if (_autoClear != null)
            {
                _autoClear.Dispose();
                _autoClear = null;
            }
        }
    }
}
