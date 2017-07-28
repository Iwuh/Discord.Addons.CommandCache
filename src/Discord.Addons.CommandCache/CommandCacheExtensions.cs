using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Discord.Addons.CommandCache
{
    public static class CommandCacheExtensions
    {
        /// <summary>
        /// Initialises and adds a command cache to the dependency map.
        /// </summary>
        /// <param name="services">The IServiceCollection that the service should be added to.</param>
        /// <param name="capacity">The maximum capacity of the cache. Must be a number greater than 0 or CommandCacheService.UNLIMITED.</param>
        /// <param name="log">A method to use for logging.</param>
        /// <returns>The client that this method was called on.</returns>
        public static DiscordSocketClient UseCommandCache(this DiscordSocketClient client, IServiceCollection services, int capacity, Func<LogMessage, Task> log)
        {
            services.AddSingleton(new CommandCacheService(client, capacity, log));
            return client;
        }

        /// <summary>
        /// Sends a message to a channel, then adds it to the command cache.
        /// </summary>
        /// <param name="cache">The command cache that the messages should be added to.</param>
        /// <param name="commandId">The ID of the command message.</param>
        /// <param name="text">The content of the message.</param>
        /// <param name="prependZWSP">Whether or not to prepend the message with a zero-width space.</param>
        /// <returns>The message that was sent.</returns>
        public static async Task<IUserMessage> SendCachedMessageAsync(this IMessageChannel channel, CommandCacheService cache, ulong commandId, string text, bool prependZWSP = false)
        {
            var message = await channel.SendMessageAsync(prependZWSP ? "\x200b" + text : text);
            cache.Add(commandId, message.Id);

            return message;
        }

        /// <summary>
        /// Adds multiple values to a ConcurrentBag.
        /// </summary>
        /// <typeparam name="T">The type of values contained in the bag.</typeparam>
        /// <param name="values">The values to add.</param>
        public static ConcurrentBag<T> AddMany<T>(this ConcurrentBag<T> bag, IEnumerable<T> values)
        {
            foreach (T item in values)
            {
                bag.Add(item);
            }
            return bag;
        }
    }
}
