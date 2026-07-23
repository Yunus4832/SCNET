using Engine.Core;

using Game.Commands;
using Game.Network.Enums;

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

    [Theory]
    [InlineData(RunModeType.Gui, WorkType.Server, true, true)]
    [InlineData(RunModeType.Gui, WorkType.Server, false, false)]
    [InlineData(RunModeType.Gui, WorkType.Client, true, false)]
    [InlineData(RunModeType.HeadlessServer, WorkType.Server, true, false)]
    public void OnlyGuiServerOwnerGetsBootstrapAuthority(
        RunModeType runMode,
        WorkType workType,
        bool isServerMaster,
        bool expected)
    {
        Assert.Equal(
            expected,
            CommandPrincipal.HasGuiServerOwnerBootstrapAuthority(
                runMode,
                workType,
                isServerMaster));
    }
}
