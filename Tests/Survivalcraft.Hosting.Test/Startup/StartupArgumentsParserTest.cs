using Game.Admission;
using Game.Protocol.Connection;
using Game.Startup;
using Game.Terrains;
using Game.World;

namespace Survivalcraft.Hosting.Test.Startup;

public class StartupArgumentsParserTest
{
    [Fact]
    public void ParsesWorldOverridesWithoutLoadingSessionsOrInstanceFiles()
    {
        var settings = new RunningSetting { RemainingArgs = ["existing"] };
        var request = StartupArgumentsParser.Parse(
            settings,
            [
                "--session", " test ", "--world", "World A", "--seed", "seed", "--game-mode", "Creative",
                "--generator", "game:flat", "--generator-param", "shape=island", "--generator-param",
                "terrainHeight=100", "--host", "--save", "custom"
            ],
            () => new ServerAdmissionOverrides()
        );
        Assert.Equal("test", request.SessionName);
        Assert.True(request.HasExplicitSession);
        Assert.Equal("World A", request.World);
        Assert.Equal("seed", request.Seed);
        Assert.Equal(GameMode.Creative, request.GameMode);
        Assert.Equal("island", request.TerrainCreation.Resolve().Get<string>("shape"));
        Assert.Equal(100d, request.TerrainCreation.Resolve().Get<double>("terrainHeight"));
        Assert.True(request.ForceWorldRunServer);
        Assert.True(request.Save);
        Assert.Equal(new[] { "existing", "custom" }, settings.RemainingArgs);
    }

    [Fact]
    public void WindowOverridesAreParsedAndRemoteConnectDisablesHosting()
    {
        var settings = new RunningSetting();
        var request = StartupArgumentsParser.Parse(
            settings,
            [
                "--window-mode", "Fullscreen", "--window-size", "1280x720", "--connect", "[::1]:28887",
                "--host"
            ],
            () => new ServerAdmissionOverrides()
        );

        Assert.Equal(StartupWindowMode.Fullscreen, settings.WindowMode);
        Assert.Equal(1280, settings.WindowWidth);
        Assert.Equal("::1", request.ConnectHost);
        Assert.Equal(28887, request.ConnectPort);
        Assert.False(request.ForceWorldRunServer);
    }

    [Fact]
    public void WorldOverridesRequireExplicitSession()
    {
        var request = StartupArgumentsParser.Parse(
            new RunningSetting(),
            [
                "--world", "Ignored", "--seed", "Ignored", "--game-mode", "Creative", "--generator", "game:flat",
                "--generator-param", "terrainHeight=100"
            ],
            () => new ServerAdmissionOverrides()
        );
        Assert.Null(request.World);
        Assert.Null(request.Seed);
        Assert.Null(request.GameMode);
        Assert.False(request.TerrainCreation.IsSpecified);
    }

    [Fact]
    public void ExplicitAdmissionModeReplacesInheritedCredentialIntent()
    {
        var request = StartupArgumentsParser.Parse(
            new RunningSetting(),
            ["--admission", "open"],
            () => new ServerAdmissionOverrides
                { Mode = ServerAdmissionMode.Password, PasswordFile = "/private/password" }
        );
        Assert.Equal(ServerAdmissionMode.Open, request.AdmissionOverrides.Mode);
        Assert.Null(request.AdmissionOverrides.PasswordFile);
    }

    [Theory]
    [InlineData("--server-password", "secret")]
    [InlineData("--join-password", "secret")]
    [InlineData("--admission", "invalid")]
    public void InvalidOrPlaintextAdmissionFailsClosed(string option, string value)
    {
        Assert.Throws<AdmissionConfigurationException>(() => StartupArgumentsParser.Parse(
                new RunningSetting(),
                [option, value],
                () => new ServerAdmissionOverrides()
            )
        );
    }

    [Fact]
    public void ParsesTransportAndHttpOverridesWithoutResolvingWorldPolicy()
    {
        var request = StartupArgumentsParser.Parse(
            new RunningSetting(),
            [
                "--server-port", "30001", "--no-broadcast", "--p2p", "true", "--room-public", "false",
                "--room-name", "Room", "--http-command", "--http-command-port", "30002", "--http-command-access-token",
                " token "
            ],
            () => new ServerAdmissionOverrides()
        );
        Assert.Equal(30001, request.ServerPort);
        Assert.Equal(-1, request.BroadcastPort);
        Assert.True(request.P2POverrides.Enabled);
        Assert.False(request.P2POverrides.IsPublic);
        Assert.Equal("Room", request.P2POverrides.Name);
        Assert.True(request.HttpCommandEnabled);
        Assert.Equal(30002, request.HttpCommandPort);
        Assert.Equal("token", request.HttpCommandAccessToken);
    }
}
