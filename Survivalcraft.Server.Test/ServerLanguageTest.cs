using System.Reflection;

using Engine.Core.FileStorage;

using Game.Configuration;
using Game.Modding;
using Game.Protocol.Localization;

namespace Survivalcraft.Server.Test;

[Collection(ServerStarterCollection.Name)]
public sealed class ServerLanguageTest
{
    [Fact]
    public void ConsoleResolvesFallbackAndTracksPublishedLanguageSnapshots()
    {
        var text = new LocalizedText("Commands", "ServerStopping_Message", "untranslated-stop");
        Assert.Equal(
            "fallback-before-startup",
            ServerStarter.TextResolver.Resolve(
                new LocalizedText("MissingSection", "MissingKey", "fallback-before-startup")
            )
        );
        Storage.RegisterFileSystemRoot("app", AppContext.BaseDirectory, readOnly: true);
        using var mods = GameModRuntime.Start();
        var runtimeField = typeof(ServerStarter).GetField(
            "_modRuntime",
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var resolverField = typeof(ServerStarter).GetField(
            "_resolver",
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var previousRuntime = runtimeField.GetValue(null);
        var previousResolver = ServerStarter.TextResolver;
        var previousCatalog = ServerStarter.LanguageCatalog;
        var previousLanguage = HostingConfiguration.Preferences.ConsoleLanguageType;
        try
        {
            runtimeField.SetValue(null, mods);
            ServerStarter.SetLanguage("zh-CN");
            var chineseResolver = ServerStarter.TextResolver;
            var chinese = chineseResolver.Resolve(text);
            Assert.NotEqual(text.Fallback, chinese);
            ServerStarter.SetLanguage("en-US");
            var englishResolver = ServerStarter.TextResolver;
            Assert.NotSame(chineseResolver, englishResolver);
            Assert.Equal("en-US", ServerStarter.LanguageCatalog.CurrentLanguage);
            Assert.NotEqual(text.Fallback, englishResolver.Resolve(text));
            Assert.NotEqual(chinese, englishResolver.Resolve(text));
            Assert.Equal(chinese, chineseResolver.Resolve(text));
        }
        finally
        {
            runtimeField.SetValue(null, previousRuntime);
            resolverField.SetValue(null, previousResolver);
            typeof(ServerStarter).GetProperty(nameof(ServerStarter.LanguageCatalog))!.SetValue(
                null,
                previousCatalog
            );
            HostingConfiguration.Preferences.ConsoleLanguageType = previousLanguage;
        }
    }
}
