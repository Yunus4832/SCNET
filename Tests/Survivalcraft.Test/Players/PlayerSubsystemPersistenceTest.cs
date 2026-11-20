using Engine.Core.Security;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Players;
using Game.Protocol.Identity;
using Game.Subsystems;

using Survivalcraft.Test.Subsystems;

namespace Game.Test.Players;

public sealed class PlayerSubsystemPersistenceTest
{
    [Fact]
    public void PlayerRecordsPersistWithoutAnyCharacterOrNetworkSubsystem()
    {
        using var project = new Project();
        var players = new SubsystemPlayers();
        WorldSubsystemTestSupport.Attach(project, players);
        var address = Address(1);
        var role = Guid.NewGuid();
        players.Store.BindCharacter(address, role);
        players.Store.Ban(address, "rule", null, DateTimeOffset.UtcNow);
        var subsystemData = new ValuesDictionary();
        players.Save(subsystemData);
        Assert.Empty(subsystemData);
        var restored = new WorldPlayerStore();
        restored.Load(project.ExtensionData.Get("game", "players"));
        Assert.Equal(role, restored.Find(address)!.CharacterId);
        Assert.Equal("rule", restored.Find(address)!.Ban!.Reason);
        Assert.Null(project.FindSubsystem<SubsystemCharacters>());
    }

    [Fact]
    public void ClientProjectionSaveDoesNotRewritePrivatePlayerRecords()
    {
        using var project = new Project();
        var players = new SubsystemPlayers();
        WorldSubsystemTestSupport.Attach(project, players);
        players.Store.GetOrCreate(Address(1));
        players.Save(new ValuesDictionary());
        var existing = project.ExtensionData.Get("game", "players");
        Assert.Single(existing);
        players.Store.GetOrCreate(Address(2));
        project.SendToClientMode = true;
        players.Save(new ValuesDictionary());
        Assert.Single(existing);
        project.SendToClientMode = false;
        players.Save(new ValuesDictionary());
        Assert.Equal(2, existing.Count);
    }

    private static PlayerAddress Address(byte value)
    {
        var bytes = new byte[32];
        bytes[0] = value;
        return new PlayerAddress(IdentityAddress.Encode(bytes));
    }
}
