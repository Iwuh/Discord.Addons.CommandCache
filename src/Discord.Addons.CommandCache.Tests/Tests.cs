using Discord.WebSocket;
using System;
using Xunit;

namespace Discord.Addons.CommandCache.Tests
{
    public class Tests : IDisposable
    {
        private CommandCacheService _cache;

        public Tests()
        {
            _cache = new CommandCacheService(new DiscordSocketClient(), 5);
        }

        public void Dispose()
        {
            _cache.Dispose();
        }

        [Fact]
        public void TestIndexer()
        {
            // Add data to the cache.
            _cache.Add(1, 2);
            _cache.Add(3, 4);

            // Assert that accessing by key returns the proper value.
            Assert.Equal<ulong>(2, _cache[1]);

            // Change a value of the cache.
            _cache[3] = 5;

            // Assert that the new value is returned.
            Assert.Equal<ulong>(5, _cache[3]);
        }

        [Fact]
        public void TestMaxSize()
        {
            // Add 5 pairs to the cache (the max size in this test).
            for (ulong i = 0; i < 5; i++)
            {
                _cache.Add(i, i);
            }

            // Add another pair.
            _cache.Add(123, 456);

            // Assert that the number of elements is still at max (i.e. the 0th element was removed to make space for the new one).
            Assert.Equal(5, _cache.Count);
        }

        [Fact]
        public void TestRemove()
        {
            _cache.Add(1, 2);

            Assert.True(_cache.Remove(1));
        }
    }
}
