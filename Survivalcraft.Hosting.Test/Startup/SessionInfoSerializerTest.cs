using System.Xml.Linq;

using Engine.Network;

using Game.Admission;
using Game.Protocol.Connection;
using Game.Servers;
using Game.Startup;
using Game.Terrains;
using Game.World;

namespace Survivalcraft.Hosting.Test.Startup;

public class SessionInfoSerializerTest
{
    [Fact]
    public void RoundTripPreservesPrivateStartupOverridesWithoutResolvingFiles()
    {
        var original = new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Name = "Named session",
            Target = SessionTarget.RemoteServer,
            World = "World",
            Seed = "seed",
            GameMode = GameMode.Creative,
            TerrainGenerationMode = TerrainGenerationMode.FlatIsland,
            TerrainLevel = 90,
            RemoteTarget = new AddressNetworkTarget("example.test", 30001),
            ServerPort = 30002,
            BroadcastPort = -1,
            AutoJoinCharacterName = "Tester",
            HttpCommandEnabled = true,
            HttpCommandPort = 30003,
            HttpCommandAccessToken = "private-token",
            AdmissionOverrides = new ServerAdmissionOverrides
            {
                Mode = ServerAdmissionMode.Password,
                PasswordFile = "/nonexistent/password-reference"
            },
            P2POverrides = new P2PRoomOverrides
            {
                Enabled = false, ControlUrl = "https://example.test/", Name = "room",
                Description = string.Empty, IsPublic = false
            }
        };
        var xml = SessionInfoSerializer.Serialize(original);
        var restored = SessionInfoSerializer.CreateDefault(string.Empty);
        SessionInfoSerializer.Populate(restored, XElement.Parse(xml.ToString()));
        Assert.Equal(original.SessionId, restored.SessionId);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Target, restored.Target);
        Assert.Equal(original.World, restored.World);
        Assert.Equal(original.Seed, restored.Seed);
        Assert.Equal(original.GameMode, restored.GameMode);
        Assert.Equal(original.TerrainGenerationMode, restored.TerrainGenerationMode);
        Assert.Equal(original.TerrainLevel, restored.TerrainLevel);
        Assert.Equal(original.RemoteTarget, restored.RemoteTarget);
        Assert.Equal(original.ServerPort, restored.ServerPort);
        Assert.Equal(original.BroadcastPort, restored.BroadcastPort);
        Assert.Equal(original.AutoJoinCharacterName, restored.AutoJoinCharacterName);
        Assert.Equal(original.HttpCommandEnabled, restored.HttpCommandEnabled);
        Assert.Equal(original.HttpCommandPort, restored.HttpCommandPort);
        Assert.Equal(original.HttpCommandAccessToken, restored.HttpCommandAccessToken);
        Assert.Equal(original.AdmissionOverrides, restored.AdmissionOverrides);
        Assert.Equal(original.P2POverrides, restored.P2POverrides);
    }

    [Theory]
    [InlineData("Address")]
    [InlineData("InvitationCode")]
    [InlineData("InvitationSession")]
    public void RemoteTargetKindsRetainTheirLogicalIdentity(string kind)
    {
        NetworkTarget target = kind switch
        {
            "Address" => new AddressNetworkTarget("example.test", 30001),
            "InvitationCode" => new InvitationCodeNetworkTarget(
                new Uri("https://example.test/"),
                "123456"
            ),
            _ => new InvitationSessionNetworkTarget(
                new Uri("https://example.test/"),
                Guid.NewGuid(),
                RelayPolicy.Required
            )
        };
        var session = new SessionInfo { Target = SessionTarget.RemoteServer, RemoteTarget = target };
        var restored = new SessionInfo();
        SessionInfoSerializer.Populate(restored, SessionInfoSerializer.Serialize(session));
        Assert.Equal(target, restored.RemoteTarget);
    }

    [Fact]
    public void MissingAndToleratedInvalidFieldsKeepExistingDefaults()
    {
        var session = new SessionInfo();
        SessionInfoSerializer.Populate(
            session,
            XElement.Parse(
                "<SessionInfo Target='bad' GameMode='bad' ServerPort='bad' HttpCommandEnabled='bad' HttpCommandPort='bad' Name=' name ' AutoJoinCharacterName=' player ' />"
            )
        );
        Assert.Equal(SessionTarget.MainMenu, session.Target);
        Assert.Null(session.GameMode);
        Assert.Equal(0, session.ServerPort);
        Assert.Null(session.HttpCommandEnabled);
        Assert.Null(session.HttpCommandPort);
        Assert.Equal("World", session.World);
        Assert.Equal("name", session.Name);
        Assert.Equal("player", session.AutoJoinCharacterName);
        Assert.Empty(session.Seed);
    }

    [Fact]
    public void InvalidIdentityFailsWithTheExistingInvalidDataClassification()
    {
        Assert.Throws<InvalidDataException>(() => SessionInfoSerializer.Populate(
                new SessionInfo(),
                XElement.Parse("<SessionInfo ServerIdentityAddress='invalid' />")
            )
        );
    }

    [Fact]
    public void InvalidTerrainKeepsPartialPopulationAndOriginalParseException()
    {
        var session = new SessionInfo();
        Assert.Throws<ArgumentException>(() => SessionInfoSerializer.Populate(
                session,
                XElement.Parse("<SessionInfo World='Already populated' TerrainGenerationMode='bad' />")
            )
        );
        Assert.Equal("Already populated", session.World);
    }

    [Fact]
    public void AdmissionRejectsUnexpectedStoredAttributes()
    {
        Assert.Throws<AdmissionConfigurationException>(() => SessionInfoSerializer.Populate(
                new SessionInfo(),
                XElement.Parse("<SessionInfo><AdmissionOverrides Password='plaintext' /></SessionInfo>")
            )
        );
    }

    [Fact]
    public void NormalizeKeepsBroadcastDisabledAndClearsNonRemoteTargets()
    {
        var session = new SessionInfo
        {
            SessionId = "invalid", Name = " name ", World = " ", ServerPort = -1, BroadcastPort = -1,
            Target = SessionTarget.World, RemoteTarget = new AddressNetworkTarget("example.test", 30001)
        };
        SessionInfoSerializer.Normalize(session);
        Assert.True(Guid.TryParse(session.SessionId, out _));
        Assert.Equal("name", session.Name);
        Assert.Equal("World", session.World);
        Assert.Equal(0, session.ServerPort);
        Assert.Equal(-1, session.BroadcastPort);
        Assert.Null(session.RemoteTarget);
    }

    [Fact]
    public void FindsSessionsByTrimmedIdAndNameInSupportedRoots()
    {
        var id = Guid.NewGuid().ToString("N");
        var single = new XElement(
            "SessionInfo",
            new XAttribute("SessionId", $" {id} "),
            new XAttribute("Name", " name ")
        );
        Assert.Same(single, SessionInfoSerializer.FindSessionElement(single, id));
        var root = new XElement("Sessions", single);
        Assert.Same(single, SessionInfoSerializer.FindSessionElementByName(root, "name"));
        Assert.Null(SessionInfoSerializer.FindSessionElement(root, "missing"));
    }
}
