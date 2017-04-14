using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Linq;
using Discord.Net;

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
            // Initialise the cache.
            _cache = new List<KeyValuePair<ulong, ulong>>();

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
                foreach (var pair in _cache)
                {
                    // The timestamp of a message can be calculated by getting the leftmost 42 bits of the ID, then
                    // adding January 1, 2015 as a Unix timestamp.
                    DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)((pair.Key >> 22) + 1420070400000UL));
                    TimeSpan difference = DateTimeOffset.UtcNow - timestamp;

                    int deletedCount = 0;
                    if (difference.TotalHours >= 2.0)
                    {
                        _cache.Remove(pair);
                        deletedCount++;
                    }

                    _logger(new LogMessage(LogSeverity.Verbose, "CommandCacheService", $"Cleaned {deletedCount} old messages from cache.")).RunSynchronously();
                }
            }, null, 7200000, 7200000);

            client.MessageDeleted += async (cacheable, channel) =>
            {
                if (ContainsKey(cacheable.Id))
                {
                    try
                    {
                        var message = await channel.GetMessageAsync(this[cacheable.Id]);
                        await message.DeleteAsync();

                        await _logger(new LogMessage(LogSeverity.Verbose, "CommandCacheService", $"{cacheable.Id} deleted, {message.Id} deleted."));
                    }
                    catch (HttpException)
                    {
                        await _logger(new LogMessage(LogSeverity.Warning, "CommandCacheService", $"{cacheable.Id} deleted but {this[cacheable.Id]} does not exist."));
                    }
                    finally
                    {
                        Remove(cacheable.Id);
                    }
                }
            };

            _logger(new LogMessage(LogSeverity.Verbose, "CommandCacheService", $"Service initialised, MessageDeleted successfully hooked.")).RunSynchronously();
        }

        ~CommandCacheService()
        {
            Dispose();
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

        /// <summary>
        /// Gets or sets the value of a pair by using the key.
        /// </summary>
        /// <param name="key">The key to search with.</param>
        /// <exception cref="KeyNotFoundException">Thrown if key is not in the cache.</exception>
        public ulong this[ulong key]
        {
            get
            {
                if (TryGetValue(key, out ulong value))
                {
                    return value;
                }

                throw new KeyNotFoundException($"The key {key} was not found in the cache.");
            }

            set
            {
                // If the pair is in the cache, remove it and add the edited version at the same location.
                if (TryGetPairByKey(key, out KeyValuePair<ulong, ulong> pair))
                {
                    var index = _cache.IndexOf(pair);
                    _cache.RemoveAt(index);
                    _cache.Insert(index, new KeyValuePair<ulong, ulong>(key, value));
                }

                // Otherwise throw an exception as the key is not in the cache.
                throw new KeyNotFoundException($"The key {key} was not found in the cache.");
            }
        }

        /// <summary>
        /// Add a new command/response pair to the cache.
        /// </summary>
        /// <param name="key">The ID of the command message.</param>
        /// <param name="value">The ID of the response message.</param>
        public void Add(ulong key, ulong value) => Add(new KeyValuePair<ulong, ulong>(key, value));

        /// <summary>
        /// Add a command/response pair to the cache.
        /// </summary>
        /// <param name="item">A KeyValuePair representing the IDs of the command and response messages.</param>
        public void Add(KeyValuePair<ulong, ulong> item)
        {
            if (_cache.Count >= _max && _max != UNLIMITED)
            {
                // If the number of items in the cache is greater than or equal to the max and the cache is not unlimited,
                // remove items starting from the zeroth element until there are (max - 1) elements in the cache.
                _cache.RemoveRange(0, (_cache.Count - _max) + 1);
            }

            // Finally, add the item.
            _cache.Add(item);
        }

        /// <summary>
        /// Clears all items from the cache.
        /// </summary>
        public void Clear() => _cache.Clear();

        /// <summary>
        /// Checks if the cache contains a specific command/response pair.
        /// </summary>
        /// <param name="item">The pair to check for.</param>
        /// <returns>Whether or not item is in the cache.</returns>
        public bool Contains(KeyValuePair<ulong, ulong> item) => _cache.Contains(item);

        /// <summary>
        /// Checks whether the cache contains a pair with a certain key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>Whether or not the key was found.</returns>
        public bool ContainsKey(ulong key) => _cache.Select(p => p.Key).Contains(key);

        /// <summary>
        /// Copies a range of the cache to an array, starting at a specified index and going until the last element.
        /// </summary>
        /// <param name="array">The array to copy to.</param>
        /// <param name="arrayIndex">The index to start copying from.</param>
        public void CopyTo(KeyValuePair<ulong, ulong>[] array, int arrayIndex) => _cache.CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<ulong, ulong>> GetEnumerator() => _cache.GetEnumerator();

        /// <summary>
        /// Removes a pair from the cache by key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>Whether or not the removal operation was successful.</returns>
        public bool Remove(ulong key)
        { 
            // If the key is in the cache, try to remove its pair.
            if (TryGetPairByKey(key, out KeyValuePair<ulong, ulong> pair))
            {
                return _cache.Remove(pair);
            }

            // Otherwise it's not in the cache so return false.
            return false;
        }

        /// <summary>
        /// Removes a pair from the cache.
        /// </summary>
        /// <param name="item">The command/response pair to remove.</param>
        /// <returns>Whether or not the removal operation was successful.</returns>
        public bool Remove(KeyValuePair<ulong, ulong> item) => _cache.Remove(item);

        /// <summary>
        /// Tries to get the value of a pair by key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <param name="value">The value of the pair (0 if the key is not found).</param>
        /// <returns>Whether or not key was found in the cache.</returns>
        public bool TryGetValue(ulong key, out ulong value)
        {
            // If the pair is in the cache, set the value and return true..
            if (TryGetPairByKey(key, out KeyValuePair<ulong, ulong> pair))
            {
                value = pair.Value;
                return true;
            }

            // Otherwise return false and set the value to 0.
            value = 0;
            return false;
        }

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

        private bool TryGetPairByKey(ulong key, out KeyValuePair<ulong, ulong> pair)
        {
            var tryPair = _cache.FirstOrDefault(p => p.Key == key);
            var defaultPair = default(KeyValuePair<ulong, ulong>);

            if (pair.Equals(defaultPair))
            {
                pair = defaultPair;
                return false;
            }

            pair = tryPair;
            return true;
        }
    }
}
