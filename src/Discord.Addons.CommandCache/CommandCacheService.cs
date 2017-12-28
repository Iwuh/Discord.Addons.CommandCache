using Discord.WebSocket;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Discord.Addons.CommandCache
{
    /// <summary>
    /// A thread-safe class used to automatically delete response messages when the command message is deleted.
    /// </summary>
    public class CommandCacheService : ICommandCache<ulong, ConcurrentBag<ulong>>, IDisposable
    {
        public const int UNLIMITED = -1; // POWEEEEEEERRRRRRRR

        private readonly ConcurrentDictionary<ulong, ConcurrentBag<ulong>> _cache
            = new ConcurrentDictionary<ulong, ConcurrentBag<ulong>>();
        private int _max;
        private Timer _autoClear;
        private Func<LogMessage, Task> _logger;
        private int _count;

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
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity can not be lower than 1 unless capacity is CommandCacheService.UNLIMITED.");
            }
            else
            {
                _max = capacity;
            }

            _autoClear = new Timer(OnTimerFired, null, 7200000, 7200000); // 7,200,000 ms = 2 hrs

            client.MessageDeleted += OnMessageDeleted;

            _logger(new LogMessage(LogSeverity.Verbose, "Command Cache", $"Service initialised, MessageDeleted successfully hooked."));
        }

        /// <summary>
        /// Gets all the keys in the cache. Will claim all locks until the operation is complete.
        /// </summary>
        public IEnumerable<ulong> Keys => _cache.Keys;

        /// <summary>
        /// Gets all the values in the cache. Will claim all locks until the operation is complete.
        /// </summary>
        public IEnumerable<ConcurrentBag<ulong>> Values => _cache.Values;

        /// <summary>
        /// Gets the number of command/response sets in the cache.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Adds a key and multiple values to the cache, or extends the existing values if the key already exists.
        /// </summary>
        /// <param name="key">The id of the command message.</param>
        /// <param name="values">The ids of the response messages.</param>
        /// <exception cref="ArgumentNullException">Thrown if values is null.</exception>
        public void Add(ulong key, ConcurrentBag<ulong> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values), "The supplied collection can not be null.");
            }

            if (_max != UNLIMITED && _count >= _max)
            {
                int removeCount = _count - _max + 1;
                // The left 42 bits represent the timestamp.
                var orderedKeys = _cache.Keys.OrderBy(k => k >> 22).ToList();
                // Remove items until we're under the maximum.
                int i = 0;
                while (i < removeCount && i < orderedKeys.Count)
                {
                    var success = Remove(orderedKeys[i]);
                    if (success) i++;
                }
                // Reset _count to _cache.Count.
                UpdateCount();
            }

            // TryAdd will return false if the key already exists, in which case we don't want to increment the count.
            if (_cache.TryAdd(key, values))
            {
                Interlocked.Increment(ref _count);
            }
            else
            {
                _cache[key].AddMany(values);
            }
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
            if (!TryGetValue(key, out ConcurrentBag<ulong> bag))
            {
                Add(key, bag = new ConcurrentBag<ulong>() { value });
            }
            else
            {
                bag.Add(value);
            }
        }

        /// <summary>
        /// Adds a key and multiple values to the cache, or extends the existing values if the key already exists.
        /// </summary>
        /// <param name="key">The id of the command message.</param>
        /// <param name="values">The ids of the response messages.</param>
        public void Add(ulong key, params ulong[] values) => Add(key, new ConcurrentBag<ulong>(values));

        /// <summary>
        /// Adds a command message and response to the cache using the message objects.
        /// </summary>
        /// <param name="command">The message that invoked the command.</param>
        /// <param name="response">The response to the command message.</param>
        public void Add(IUserMessage command, IUserMessage response) => Add(command.Id, response.Id);

        /// <summary>
        /// Clears all items from the cache. Will claim all locks until the operation is complete.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            Interlocked.Exchange(ref _count, 0);
        }

        /// <summary>
        /// Checks whether the cache contains a set with a certain key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>Whether or not the key was found.</returns>
        public bool ContainsKey(ulong key) => _cache.ContainsKey(key);

        public IEnumerator<KeyValuePair<ulong, ConcurrentBag<ulong>>> GetEnumerator() => _cache.GetEnumerator();

        /// <summary>
        /// Removes a set from the cache by key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>Whether or not the removal operation was successful.</returns>
        public bool Remove(ulong key)
        {
            var success = _cache.TryRemove(key, out ConcurrentBag<ulong> _);
            if (success) Interlocked.Decrement(ref _count);
            return success;
        }

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
            Dispose(true);
        }

        protected void Dispose(bool disposing)
        {
            if (disposing && _autoClear != null)
            {
                _autoClear.Dispose();
                _autoClear = null;
            }
        }

        private void OnTimerFired(object state)
        {
            // Get all messages where the timestamp is older than 2 hours, then convert it to a list. The result of where merely contains references to the original
            // collection, so iterating and removing will throw an exception. Converting it to a list first avoids this.
            var purge = _cache.Where(p =>
            {
                // The timestamp of a message can be calculated by getting the leftmost 42 bits of the ID, then
                // adding January 1, 2015 as a Unix timestamp.
                DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)((p.Key >> 22) + 1420070400000UL));
                TimeSpan difference = DateTimeOffset.UtcNow - timestamp;

                return difference.TotalHours >= 2.0;
            }).ToList();

            var removed = purge.Where(p => Remove(p.Key));

            UpdateCount();

            _logger(new LogMessage(LogSeverity.Verbose, "Command Cache", $"Cleaned {removed.Count()} items from the cache."));
        }

        private async Task OnMessageDeleted(Cacheable<IMessage, ulong> cacheable, ISocketMessageChannel channel)
        {
            if (TryGetValue(cacheable.Id, out ConcurrentBag<ulong> messages))
            {
                foreach (var messageId in messages)
                {
                    var message = await channel.GetMessageAsync(messageId);
                    if (message != null)
                    {
                        await message.DeleteAsync();
                    }
                    else
                    {
                        await _logger(new LogMessage(LogSeverity.Warning, "Command Cache", $"{cacheable.Id} deleted but {messageId} does not exist."));
                    }
                    Remove(cacheable.Id);
                    Interlocked.Decrement(ref _count);
                }
            }
        }

        private void UpdateCount() => Interlocked.Exchange(ref _count, _cache.Count);
    }
}
