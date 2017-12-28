using Discord;
using Discord.Addons.CommandCache;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ExampleBot
{
    public class TestModule : CommandCacheModuleBase<SocketCommandContext>
    {
        [Command("add")]
        [Summary("Adds two whole numbers together.")]
        public async Task Add(int first, int second)
        {
            // Deleting the command message will automatically delete the response message sent by the line below.
            await ReplyAsync($"{first} plus {second} is {first + second}.");
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
            Cache.Add(Context.Message.Id, message.Id);
        }

        [Command("shutdown", RunMode = RunMode.Async)]
        [Summary("Shuts down the bot.")]
        public async Task Shutdown()
        {
            Cache.Dispose();

            await Context.Client.StopAsync();
            await Context.Client.LogoutAsync();

            Environment.Exit(0);
        }
    }
}
