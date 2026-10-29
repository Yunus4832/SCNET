using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

using Content.Packaging;

using Engine.Graphics;

using Game.Help;
using Game.Managers;
using Game.Modding;

using Rendezvous.Protocol;

using ServerSource.Protocol;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class AboutHelpTopicTest
{
    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("pt-PT")]
    [InlineData("ru-RU")]
    public void AboutReadsRunningAssembliesAndActualFormatVersions(string culture)
    {
        ContentManager.Initialize();
        using var runtime = GameModRuntime.Start();
        runtime.InitializeLanguage(culture);
        var topic = HelpTopic.Create("52", new JsonObject { ["Name"] = "About" });
        var text = topic.Text;
        Assert.Contains(AboutHelpTopic.ShortBuildVersion(VersionsManager.BuildVersion), text);
        Assert.Contains(
            typeof(Texture2D).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                .InformationalVersion.Split('+', 2)[0],
            text
        );
        foreach (var (key, value) in new[]
                 {
                     ("GameProtocol", VersionsManager.ProtocolVersion.ToString()),
                     ("ServerSourceProtocol", ServerSourceProtocol.CurrentVersion.ToString()),
                     ("SignalingProtocol", SignalCodec.CurrentVersion.ToString()),
                     ("RelayProtocol", RelayDatagramCodec.CurrentVersion.ToString()),
                     ("PackageFormat", ContentPackageManifest.CurrentFormatVersion.ToString())
                 })
        {
            Assert.Contains($"{LanguageManager.Get("About", key)}: {value}", text);
        }

        Assert.Contains(RuntimeInformation.ProcessArchitecture.ToString(), text);
        Assert.Contains(AboutHelpTopic.SourceUrl, text);
        Assert.DoesNotContain("[Help:", text);
        Assert.Equal("[Help:5:value]", HelpTopic.Create("5", new JsonObject { ["Name"] = "Keyboard" }).Text);
        Assert.Equal("About", LanguageManager.GetHelpTopic("52", "Name"));
        Assert.Equal("Disclaimer", LanguageManager.GetHelpTopic("51", "Name"));
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("pt-PT")]
    [InlineData("ru-RU")]
    public void AdmissionAndIdentityHelpFollowP2PInEveryLanguage(string culture)
    {
        ContentManager.Initialize();
        using var runtime = GameModRuntime.Start();
        runtime.InitializeLanguage(culture);
        var topics = (JsonObject)LanguageManager.KeyWords["Help"]!;
        var names = topics.Select(pair => (string?)pair.Value?["Name"]).ToArray();
        var p2p = Array.IndexOf(names, "P2P");
        Assert.True(p2p >= 0);
        Assert.Equal(new[] { "P2P", "Admission", "PlayerIdentity" }, names.Skip(p2p).Take(3));
        foreach (var key in new[] { "49", "50" })
        {
            Assert.False(string.IsNullOrWhiteSpace(LanguageManager.GetHelpTopic(key, "Title")));
            Assert.False(string.IsNullOrWhiteSpace(LanguageManager.GetHelpTopic(key, "value")));
        }
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("pt-PT")]
    [InlineData("ru-RU")]
    public void LocalizedHelpBodiesRenderWithoutBrokenEscapeSequences(string culture)
    {
        ContentManager.Initialize();
        using var runtime = GameModRuntime.Start();
        runtime.InitializeLanguage(culture);
        var topics = (JsonObject)LanguageManager.KeyWords["Help"]!;
        foreach (var (key, topic) in topics)
        {
            if (topic?["Name"]?.GetValue<string>() is "Keyboard" or "Gamepad")
            {
                continue;
            }

            var text = LanguageManager.GetHelpTopic(key, "value");
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotMatch(@"\\(?:\s|[nrt])", text);
        }
    }

    [Theory]
    [InlineData("0.1.2+123456789abcdef", "0.1.2+12345678")]
    [InlineData("0.1.2-beta.1+abcdef0123456789", "0.1.2-beta.1+abcdef01")]
    [InlineData("0.1.2+1234", "0.1.2+1234")]
    [InlineData("0.1.2", "0.1.2")]
    public void DisplayUsesAtMostEightRevisionCharacters(string full, string expected)
    {
        Assert.Equal(expected, AboutHelpTopic.ShortBuildVersion(full));
    }

    [Fact]
    public void SignalCodecUsesTheDisplayedWireVersionAndRejectsOthers()
    {
        var bytes = SignalCodec.Encode(new SignalMessage("probe", Guid.NewGuid()));
        Assert.Equal(SignalCodec.CurrentVersion, bytes[4]);
        Assert.True(SignalCodec.TryDecode(bytes, out _));
        bytes[4]++;
        Assert.False(SignalCodec.TryDecode(bytes, out _));
    }

    [Fact]
    public void RelayCodecUsesTheDisplayedWireVersionAndRejectsOthers()
    {
        var key = new byte[32];
        var bytes = RelayDatagramCodec.Encode(Guid.NewGuid(), 0, 1, key, new byte[] { 1 });
        Assert.Equal(RelayDatagramCodec.CurrentVersion, bytes[4]);
        Assert.True(RelayDatagramCodec.TryDecode(bytes, key, out _));
        bytes[4]++;
        Assert.False(RelayDatagramCodec.TryDecode(bytes, key, out _));
    }
}
