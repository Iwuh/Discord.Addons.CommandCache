using Discord.Commands;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Discord.Addons.CommandCache
{
    /// <summary>
    /// An extension of <see cref="ModuleBase{T}"/> that facilitates use of any <see cref="ICommandCache{TKey, TValue}"/> implementation.
    /// </summary>
    /// <typeparam name="TCommandCache">The <see cref="ICommandCache{TKey, TValue}"/> implementation to use.</typeparam>
    /// <typeparam name="TCacheKey">The type of the cache's key.</typeparam>
    /// <typeparam name="TCacheValue">The type of the cache's value.</typeparam>
    /// <typeparam name="TCommandContext">The <see cref="ICommandContext"/> implementation to use.</typeparam>
    public abstract class CommandCacheModuleBase<TCommandCache, TCacheKey, TCacheValue, TCommandContext> : ModuleBase<TCommandContext>
        where TCommandCache : ICommandCache<TCacheKey, TCacheValue>
        where TCommandContext : class, ICommandContext
    {
        public TCommandCache Cache { get; set; }

        /// <summary>
        /// Sends a message to the channel the command was invoked in, and adds the response to the cache.
        /// </summary>
        /// <param name="message">The message's contents.</param>
        /// <param name="isTTS">Whether or not the message should use text to speech.</param>
        /// <param name="embed">The message's rich embed.</param>
        /// <param name="options">Options to modify the API request.</param>
        /// <returns>The response message that was sent.</returns>
        protected async override Task<IUserMessage> ReplyAsync(string message, bool isTTS = false, Embed embed = null, RequestOptions options = null)
        {
            var response = await Context.Channel.SendMessageAsync(message, isTTS, embed, options).ConfigureAwait(false);
            Cache.Add(Context.Message, response);
            return response;
        }
    }

    /// <summary>
    /// An extension of <see cref="ModuleBase{T}"/> that facilitates use of <see cref="CommandCacheService"/>.
    /// </summary>
    /// <typeparam name="TCommandContext">The <see cref="ICommandContext"/> implementation to use.</typeparam>
    public abstract class CommandCacheModuleBase<TCommandContext> : CommandCacheModuleBase<CommandCacheService, ulong, ConcurrentBag<ulong>, TCommandContext>
        where TCommandContext : class, ICommandContext
    { }
}
