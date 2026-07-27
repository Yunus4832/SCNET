using Game.Commands;

namespace Survivalcraft.Test.Commands;

public class CommandPermissionSetTest
{
    [Fact]
    public void DirectAndDelegableGrantsHaveDifferentCapabilities()
    {
        var permissions = new CommandPermissionSet();
        permissions.Grant("world.time.set", false);
        permissions.Grant("player.*", true);

        Assert.True(permissions.HasPermission("world.time.set"));
        Assert.False(permissions.CanDelegate("world.time.set"));
        Assert.True(permissions.HasPermission("player.moderate"));
        Assert.True(permissions.CanDelegate("player.moderate"));
        Assert.False(permissions.CanDelegate("world.time.set"));
    }

    [Fact]
    public void PrincipalCanOpenGrantCommandOnlyWithDelegableScope()
    {
        var direct = new CommandPrincipal(
            "Direct",
            permissions: ["world.time.set"]);
        var delegator = new CommandPrincipal(
            "Delegator",
            permissions: ["world.*"],
            delegablePermissions: ["world.*"]);
        var directWildcard = new CommandPrincipal(
            "DirectWildcard",
            permissions: ["*"]);

        Assert.False(direct.HasPermission(CommandPermissionSet.GrantPermission));
        Assert.False(directWildcard.HasPermission(CommandPermissionSet.GrantPermission));
        Assert.True(delegator.HasPermission(CommandPermissionSet.GrantPermission));
        Assert.True(delegator.CanDelegate("world.time.set"));
        Assert.False(delegator.CanDelegate("server.stop"));
    }

    [Fact]
    public void StandardPermissionManagerDoesNotImplicitlyReceiveCommandPermissions()
    {
        var manager = new CommandPrincipal(
            "Manager",
            permissions: [CommandPermissionSet.ManageStandardPermission]);
        var delegatingManager = new CommandPrincipal(
            "DelegatingManager",
            permissions: [CommandPermissionSet.ManageStandardPermission],
            delegablePermissions: [CommandPermissionSet.ManageStandardPermission]);

        Assert.True(manager.HasPermission(CommandPermissionSet.GrantPermission));
        Assert.False(manager.HasPermission("world.time.set"));
        Assert.False(manager.CanDelegate(CommandPermissionSet.ManageStandardPermission));
        Assert.True(delegatingManager.HasPermission(CommandPermissionSet.GrantPermission));
        Assert.True(delegatingManager.CanDelegate(CommandPermissionSet.ManageStandardPermission));
    }

    [Fact]
    public void GrantUpgradesButDoesNotSilentlyDowngradeDelegation()
    {
        var permissions = new CommandPermissionSet();

        Assert.True(permissions.Grant("world.time.set", false));
        Assert.True(permissions.Grant("world.time.set", true));
        Assert.False(permissions.Grant("world.time.set", false));

        var grant = Assert.Single(permissions.Grants);
        Assert.True(grant.CanDelegate);
    }

    [Fact]
    public void PermissionsRoundTripThroughValuesDictionary()
    {
        var source = new CommandPermissionSet();
        source.Grant("server.stop", false);
        source.Grant("world.*", true);

        var clone = new CommandPermissionSet();
        clone.Load(source.Save());

        Assert.Equal(source.Grants, clone.Grants);
    }

    [Theory]
    [InlineData("world*")]
    [InlineData("world*other.*")]
    [InlineData("world.*.set")]
    [InlineData("world..set")]
    [InlineData("world set")]
    public void InvalidPermissionNodesAreRejected(string permission)
    {
        Assert.Throws<ArgumentException>(() => CommandPermissionSet.Normalize(permission));
    }

}
