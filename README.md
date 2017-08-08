# Discord.Addons.CommandCache
A Discord.Net addon to autodelete command responses when the command message is deleted.

# Usage
The built in `CommandCacheService` stores message IDs, and is thread safe. It should fit most use cases, but you can create your own implementation using `ICommandCache`.
## Adding the service
Either add it manually:
```cs
var services = new ServiceCollection();
services.AddSingleton(new CommandCacheService(_client));
```
Or use the extension method:
```cs
var services = new ServiceCollection();
_client = new DiscordSocketClient().UseCommandCache(services, 500, Log);
```
You can also have an instance with no size limit by passing `CommandCacheService.UNLIMITED` as `capacity`.

## Adding values to the cache
When responding to a command, there are two ways to add to the cache. The first is by using the `Add` method:
```cs
var responseMessage = await ReplyAsync("This is a command response");
_cache.Add(Context.Message.Id, responseMessage.Id);
```
The second is by using the `SendCachedMessageAsync` extension method:
```cs
await Context.Channel.SendCachedMessageAsync(_cache, Context.Message.Id, "This is a command response");
```

The built in `CommandCacheService` supports multiple response messages per command message, however if you want to add multiple at once you cannot use the extension method.

## Disposing of the cache
The built in `CommandCacheService` uses a `System.Threading.Timer` internally, so you should call the `Dispose` method in your shutdown logic.

## Example
Refer to the [example bot](https://github.com/Iwuh/Discord.Addons.CommandCache/tree/master/src/ExampleBot) for proper usage of the command cache.
