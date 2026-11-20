using Game.Components;
using Game.Localization;
using Game.Managers;
using Game.Modding;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Messaging;

[Collection(ConfigFileCollection.Name)]
public sealed class CombatEffectLocalizationTest
{
    [Theory]
    [InlineData("zh-CN", "未命中")]
    [InlineData("en-US", "Miss")]
    [InlineData("pt-PT", "Falha")]
    [InlineData("ru-RU", "Промах")]
    public void MissEffectResolvesUsingReceivingClientsActualLanguage(string culture, string expected)
    {
        ContentManager.Initialize();
        using var runtime = GameModRuntime.Start();
        var effect = new CharacterHitEffectPackage { Text = new LocalizedText(ComponentMiner.Name, "2", "2") };
        var received = Assert.IsType<CharacterHitEffectPackage>(
            ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(effect))
        );
        var catalog = runtime.Content.CreateLanguageCatalog(culture);
        var resolver = new LocalizedTextResolver(catalog);
        Assert.Equal(expected, resolver.Resolve(received.Text));
        Assert.False(received.Text.IsLiteral);
        Assert.Equal(ComponentMiner.Name, received.Text.Section);
    }
}
