using System.Text.Json.Nodes;

using Game.Localization;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Messaging;

[Collection(ConfigFileCollection.Name)]
public sealed class RuleTextLocalizationTest
{
    [Fact]
    public void CanonicalCommandResponseRetainsNestedResourcesAcrossLocales()
    {
        var previous = ClientLanguage.Catalog.KeyWords;
        var text = LocalizedText.Format(
            new LocalizedText("Commands", "Envelope", "Season {0}"),
            new LocalizedText("Commands", "Season", "summer")
        );
        var result = new Game.Commands.CommandResult(true, "season", text);
        var response = CommandResponseBuilder.Capture(result);
        var encoded = ProtocolMessageCodec.Encode(CommandPackage.CreateResult(response, "request"));
        try
        {
            ClientLanguage.Catalog.KeyWords = new JsonObject
                { ["Commands"] = new JsonObject { ["Envelope"] = "Season {0}", ["Season"] = "summer" } };
            Assert.Equal("Season summer", Game.Commands.CommandText.Resolve(result));
            ClientLanguage.Catalog.KeyWords = new JsonObject
                { ["Commands"] = new JsonObject { ["Envelope"] = "季节 {0}", ["Season"] = "夏季" } };
            Assert.Equal("季节 夏季", Game.Commands.CommandText.Resolve(result));
            Assert.Same(text, response.Message);
            Assert.Equal(
                encoded,
                ProtocolMessageCodec.Encode(CommandPackage.CreateResult(response, "request"))
            );
        }
        finally { ClientLanguage.Catalog.KeyWords = previous; }
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("5")]
    public void SleepRefusalResourceResolvesForReceivingLocale(string key)
    {
        var previous = ClientLanguage.Catalog.KeyWords;
        var source = new LocalizedText("ComponentSleep", key, key);
        try
        {
            ClientLanguage.Catalog.KeyWords = new JsonObject
                { ["ComponentSleep"] = new JsonObject { [key] = "cannot sleep" } };
            Assert.Equal("cannot sleep", ClientLanguage.Resolver.Resolve(source));
            ClientLanguage.Catalog.KeyWords = new JsonObject
                { ["ComponentSleep"] = new JsonObject { [key] = "无法睡觉" } };
            Assert.Equal("无法睡觉", ClientLanguage.Resolver.Resolve(source));
            Assert.Equal(key, source.Key);
        }
        finally { ClientLanguage.Catalog.KeyWords = previous; }
    }

    [Fact]
    public void WidgetCauseResolvesAtDisplayForEachLocale()
    {
        var previous = ClientLanguage.Catalog.KeyWords;
        var source = LocalizedText.ContentWidget("VitalStatsWidget", "Choked", "Choked");
        try
        {
            ClientLanguage.Catalog.KeyWords =
                JsonNode.Parse("{\"ContentWidgets\":{\"VitalStatsWidget\":{\"Choked\":\"Choked\"}}}")!.AsObject();
            Assert.Equal("Choked", ClientLanguage.Resolver.Resolve(source));
            ClientLanguage.Catalog.KeyWords =
                JsonNode.Parse("{\"ContentWidgets\":{\"VitalStatsWidget\":{\"Choked\":\"窒息\"}}}")!.AsObject();
            Assert.Equal("窒息", ClientLanguage.Resolver.Resolve(source));
            Assert.Equal(LocalizedTextKind.ContentWidget, source.Kind);
            Assert.Equal("VitalStatsWidget", source.Section);
            Assert.Equal("Choked", source.Key);
        }
        finally { ClientLanguage.Catalog.KeyWords = previous; }
    }

    [Fact]
    public void SameDeathFactResolvesForDifferentLocalesWithoutMutation()
    {
        var previous = ClientLanguage.Catalog.KeyWords;
        var source = LocalizedText.Format(
            new LocalizedText("Health", "Death", "{0} died"),
            LocalizedText.Database("DisplayName", "Wolf", "Wolf")
        );
        var saved = LocalizedTextStorage.Save(source);
        try
        {
            ClientLanguage.Catalog.KeyWords = JsonNode.Parse(
                "{\"Health\":{\"Death\":\"{0} died\"},\"Database\":{\"DisplayName\":{\"Wolf\":\"Wolf\"}}}"
            )!.AsObject();
            Assert.Equal("Wolf died", ClientLanguage.Resolver.Resolve(source));
            ClientLanguage.Catalog.KeyWords = JsonNode.Parse(
                "{\"Health\":{\"Death\":\"{0} 死亡\"},\"Database\":{\"DisplayName\":{\"Wolf\":\"狼\"}}}"
            )!.AsObject();
            Assert.Equal("狼 死亡", ClientLanguage.Resolver.Resolve(source));
            Assert.Equal(source, LocalizedTextStorage.Load(saved));
        }
        finally { ClientLanguage.Catalog.KeyWords = previous; }
    }
}
