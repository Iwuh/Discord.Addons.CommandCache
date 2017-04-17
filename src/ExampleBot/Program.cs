using Discord;
using Discord.Addons.CommandCache;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ExampleBot
{
    class Program
    {
        static void Main(string[] args) => new Program().StartAsync().GetAwaiter().GetResult();

        private DiscordSocketClient _client;
        private DependencyMap _map;
        private CommandService _commands;

        public async Task StartAsync()
        {
            _map = new DependencyMap();

            _client = new DiscordSocketClient().UseCommandCache(_map, 200, Log);
            _map.Add(_client);

            _commands = new CommandService();
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());

            _client.Log += Log;
            _client.MessageReceived += HandleCommand;

            await _client.LoginAsync(TokenType.Bot, "YOUR TOKEN HERE");
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private Task Log(LogMessage message)
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }

        private async Task HandleCommand(SocketMessage message)
        {
            var userMessage = message as SocketUserMessage;
            if (userMessage == null) return;
            if (userMessage.Author.IsBot) return;

            int argPos = 0;
            if (userMessage.HasCharPrefix('+', ref argPos))
            {
                var context = new CommandContext(_client, userMessage);
                await _commands.ExecuteAsync(context, argPos, _map);
            }
        }
    }
}