using EntitySystem.TemplatesDatabase;

using Game.Identity;
using Game.Managers;
using Game.Servers;
using Game.Startup;
using Game.World;

namespace Survivalcraft.Test.Servers;

public sealed class ServerIdentitySelectionTest
{
    private readonly LocalIdentityManager _identities;

    public ServerIdentitySelectionTest()
    {
        _identities = new LocalIdentityManager(new LocalIdentitySettings(), _ => { });
        _identities.EnsureDefaults();
    }

    [Fact]
    public void SessionOverridesWorldWhichOverridesInstanceDefaultWithoutChangingEither()
    {
        var worldAddress = _identities.Create(IdentityPurpose.Server, "World");
        var sessionAddress = _identities.Create(IdentityPurpose.Server, "Session");
        var world = new WorldSettings();
        Assert.Equal(
            _identities.GetSelectedAddress(IdentityPurpose.Server),
            ServerIdentitySelection.Resolve(_identities, null, world)
        );
        world.ServerIdentityAddress = worldAddress;
        Assert.Equal(worldAddress, ServerIdentitySelection.Resolve(_identities, null, world));
        var session = new SessionInfo { ServerIdentityAddress = sessionAddress };
        Assert.Equal(sessionAddress, ServerIdentitySelection.Resolve(_identities, session, world));
        Assert.Equal(worldAddress, world.ServerIdentityAddress);
        Assert.NotEqual(sessionAddress, _identities.GetSelectedAddress(IdentityPurpose.Server));
        _identities.Delete(IdentityPurpose.Server, sessionAddress);
        Assert.Throws<InvalidOperationException>(() => ServerIdentitySelection.Resolve(_identities, session, world));
    }

    [Fact]
    public void WorldStoresOnlyPublicIdentitySelection()
    {
        var address = _identities.Create(IdentityPurpose.Server, "World");
        var world = new WorldSettings { ServerIdentityAddress = address };
        var values = new ValuesDictionary();
        world.Save(values, false);
        Assert.Equal(address, values.GetValue<string>(nameof(WorldSettings.ServerIdentityAddress)));
        Assert.False(values.ContainsKey("PrivateKey"));
        var restored = new WorldSettings();
        restored.Load(values);
        Assert.Equal(address, restored.ServerIdentityAddress);
    }
}
