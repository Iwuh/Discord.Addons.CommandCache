using Discord.Commands;
using Discord.WebSocket;
using System;
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
        /// <param name="map">The map that the service should be added to.</param>
        /// <param name="capacity">The maximum capacity of the cache.</param>
        /// <param name="log">A method to use for logging.</param>
        /// <returns>The client that this method was called on.</returns>
        public static DiscordSocketClient UseCommandCache(this DiscordSocketClient client, IDependencyMap map, int capacity, Func<LogMessage, Task> log)
        {
            map.Add(new CommandCacheService(client, capacity, log));
            return client;
        }

        public static async Task<IUserMessage> SendCachedMessageAsync(this IMessageChannel channel, CommandCacheService cache, ulong commandId, string text = "", bool prependZWSP = false)
        {
            var message = await channel.SendMessageAsync(prependZWSP ? "\x200b" + text : text);
            cache.Add(commandId, message.Id);

            return message;
        }
    }
}
