using System.Text.Json.Nodes;

using Game.Localization;
using Game.Messaging;
using Game.Protocol.Localization;
using Game.Protocol.Messaging;

namespace Survivalcraft.Core.Test.Localization;

public sealed class LocaleInstanceTest
{
    [Fact]
    public void IndependentHostAndClientCatalogsUseTheSameResolverWithoutSharedState()
    {
        var host = new LanguageCatalog { KeyWords = JsonNode.Parse("{\"Rule\":{\"Text\":\"host\"}}")!.AsObject() };
        var client = new LanguageCatalog { KeyWords = JsonNode.Parse("{\"Rule\":{\"Text\":\"client\"}}")!.AsObject() };
        var text = new LocalizedText("Rule", "Text", "fallback");
        Assert.Equal("host", new LocalizedTextResolver(host).Resolve(text));
        Assert.Equal("client", new LocalizedTextResolver(client).Resolve(text));
        client.KeyWords.Clear();
        Assert.Equal("host", new LocalizedTextResolver(host).Resolve(text));
        Assert.Equal("fallback", new LocalizedTextResolver(client).Resolve(text));
    }

    [Fact]
    public void PresenceAllowsTranslationsEqualToTheirKeyAndSection()
    {
        var catalog = new LanguageCatalog
        {
            KeyWords = JsonNode.Parse("{\"Rule\":{\"Key\":\"Key\",\"Section\":\"Rule\"}}")!.AsObject()
        };
        var resolver = new LocalizedTextResolver(catalog);
        Assert.True(catalog.TryGet(out var value, "Rule", "Key"));
        Assert.Equal("Key", value);
        Assert.Equal("Key", resolver.Get("Rule", "Key", "fallback"));
        Assert.Equal("Rule", resolver.Get("Rule", "Section", "fallback"));
        Assert.Equal("fallback", resolver.Get("Rule", "Missing", "fallback"));
    }

    [Fact]
    public void CpuMessageFormattingUsesTheProvidedCatalogWithRealPresence()
    {
        var catalog = new LanguageCatalog
        {
            KeyWords = JsonNode.Parse("{\"MultiplayerUI\":{\"SystemPrefix\":\"SystemPrefix\"}}")!.AsObject()
        };
        var formatter = new GameMessageFormatter(new LocalizedTextResolver(catalog));
        var result = formatter.Format(GameMessage.System("body"));
        Assert.Equal("SystemPrefix", result.Segments[0].Text.Fallback);
        Assert.DoesNotContain(
            typeof(LanguageCatalog).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is "Survivalcraft.Client" or "Survivalcraft"
        );
    }
}
