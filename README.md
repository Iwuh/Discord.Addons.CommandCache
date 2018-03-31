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

## Using the cache in a module
In order to utilise the command cache, you must add it to a module. While this can be done manually through Discord.Net's dependency injection, an easier method is to have your module implement `CommandCacheModuleBase<TCommandCache, TCacheKey, TCacheValue, TCommandContext>`. This works with any implementation of `ICommandCache<TKey, TValue>`. 

Because that's a quite verbose class name, this package also provides `CommandCacheModuleBase<TCommandContext>` which uses the default implementation of `ICommandCache<TKey, TValue>`.

You can then access the cache through the module's `Cache` property. Responding to commands through `ReplyAsync` will automatically add the messages to the cache.

## Adding values to the cache outside modules
There are two ways to add a message to the cache without the `ModuleBase` extension. The first is by using the `Add` method:
```cs
var responseMessage = await ReplyAsync("This is a command response");
_cache.Add(Context.Message.Id, responseMessage.Id);
```
The second is by using the `SendCachedMessageAsync` extension method:
```cs
await Context.Channel.SendCachedMessageAsync(_cache, Context.Message.Id, "This is a command response");
```
This method only works with the default `CommandCacheService` and does not support adding multiple messages at once.

## Disposing of the cache
The built in `CommandCacheService` uses a `System.Threading.Timer` internally, so you should call the `Dispose` method in your shutdown logic.

## Example
Refer to the [example bot](https://github.com/Iwuh/Discord.Addons.CommandCache/tree/master/src/ExampleBot) for proper usage of the command cache.
