using Discord;
using Discord.Addons.CommandCache;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ExampleBot
{
    public class TestModule : ModuleBase
    {
        private CommandCacheService _cache;

        public TestModule(CommandCacheService ccs)
        {
            _cache = ccs;
        }

        [Command("add")]
        [Summary("Adds two whole numbers together.")]
        public async Task Add(int first, int second)
        {
            // Deleting the command message will automatically delete the response message sent by the line below.
            await Context.Message.Channel.SendCachedMessageAsync(_cache, Context.Message.Id, $"{first} plus {second} is {first + second}.");
        }

        [Command("info")]
        [Summary("Gets information about the bot.")]
        [Alias("stats")]
        public async Task GetInfo()
        {
            var embed = new EmbedBuilder()
                .WithTitle("Bot stats:")
                .AddField(f => f.WithName("Heap size:").WithValue($"{GC.GetTotalMemory(false) / 1024.0f / 1024.0f} MB").WithIsInline(true))
                .AddField(f => f.WithName("Discord.Net Version:").WithValue(DiscordConfig.Version).WithIsInline(true))
                .AddField(f => f.WithName("Total Guilds:").WithValue((Context.Client as DiscordSocketClient).Guilds.Count).WithIsInline(true));

            var message = await ReplyAsync(string.Empty, embed: embed);
            _cache.Add(Context.Message.Id, message.Id);
        }
    }
}
