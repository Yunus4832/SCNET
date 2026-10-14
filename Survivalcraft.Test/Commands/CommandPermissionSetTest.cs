using Game.Commands;
using Game.Modding;

namespace Survivalcraft.Test.Commands;

public class CommandPermissionSetTest
{
    private static readonly ModId _owner = new("example.commands");

    [Fact]
    public void DirectAndDelegableGrantsHaveDifferentCapabilities()
    {
        var use = Permission("world.time.set");
        var delegatePermission = Permission("player.moderate");
        var permissions = new CommandPermissionSet();
        permissions.Grant(use, false);
        permissions.Grant(delegatePermission, true);

        Assert.True(permissions.HasPermission(use));
        Assert.False(permissions.CanDelegate(use));
        Assert.True(permissions.HasPermission(delegatePermission));
        Assert.True(permissions.CanDelegate(delegatePermission));
    }

    [Fact]
    public void GrantUpgradesButDoesNotSilentlyDowngradeDelegation()
    {
        var permission = Permission("world.time.set");
        var permissions = new CommandPermissionSet();

        Assert.True(permissions.Grant(permission, false));
        Assert.True(permissions.Grant(permission, true));
        Assert.False(permissions.Grant(permission, false));

        Assert.True(Assert.Single(permissions.Grants).CanDelegate);
    }

    [Fact]
    public void PermissionsRoundTripThroughValuesDictionary()
    {
        var source = new CommandPermissionSet();
        source.Grant(Permission("server.status"), false);
        source.Grant(Permission("world.time.set"), true);

        var clone = new CommandPermissionSet();
        clone.Load(source.Save());

        Assert.Equal(source.Grants, clone.Grants);
    }

    [Fact]
    public void RegistrySeparatesStandardOperatorManagedAndNonGrantable()
    {
        var registry = new CommandPermissionRegistry();
        var standard = Permission("world.time.set");
        var managed = Permission("server.player.kick");
        var operatorOnly = Permission("server.stop");
        registry.Register(
            _owner,
            standard,
            new CommandPermissionDefinition(CommandDomain.World));
        registry.Register(
            _owner,
            managed,
            new CommandPermissionDefinition(
                CommandDomain.Server,
                PermissionGrantPolicy.OperatorManaged));
        registry.Register(
            _owner,
            operatorOnly,
            new CommandPermissionDefinition(
                CommandDomain.Server,
                PermissionGrantPolicy.NonGrantable));
        var delegator = CommandPrincipal.CreatePlayer(
            "Delegator",
            permissions: [standard],
            delegablePermissions: [standard]);

        Assert.True(registry.CanGrant(standard, delegator, null));
        Assert.False(registry.CanGrant(managed, delegator, null));
        Assert.False(registry.CanGrant(operatorOnly, delegator, null));
        Assert.True(registry.CanGrant(
            managed,
            CommandPrincipal.LocalOperator,
            null));
        Assert.False(registry.CanGrant(
            operatorOnly,
            CommandPrincipal.LocalOperator,
            null));
    }

    [Fact]
    public void NonGrantablePermissionCannotBeSmuggledThroughPlayerGrant()
    {
        var registry = new CommandPermissionRegistry();
        var permission = Permission("server.stop");
        registry.Register(
            _owner,
            permission,
            new CommandPermissionDefinition(
                CommandDomain.Server,
                PermissionGrantPolicy.NonGrantable));
        var player = CommandPrincipal.CreatePlayer(
            "Player",
            permissions: [permission]);

        Assert.False(registry.HasEffectivePermission(permission, player, null));
        Assert.True(registry.HasEffectivePermission(
            permission,
            CommandPrincipal.LocalOperator,
            null));
    }

    [Fact]
    public void StandardManagerCannotDelegateBeyondItsOwnDelegationLevel()
    {
        var registry = new CommandPermissionRegistry();
        var managerPermission = Permission("permissions.manage.standard");
        var target = Permission("world.time.set");
        registry.Register(
            _owner,
            managerPermission,
            new CommandPermissionDefinition(
                CommandDomain.Server,
                managesStandardPermissions: true));
        registry.Register(
            _owner,
            target,
            new CommandPermissionDefinition(CommandDomain.World));
        var useOnlyManager = CommandPrincipal.CreatePlayer(
            "Manager",
            permissions: [managerPermission]);
        var delegatingManager = CommandPrincipal.CreatePlayer(
            "DelegatingManager",
            permissions: [managerPermission],
            delegablePermissions: [managerPermission]);

        Assert.True(registry.CanGrant(target, useOnlyManager, null));
        Assert.False(registry.CanGrant(
            target,
            useOnlyManager,
            null,
            canDelegate: true));
        Assert.True(registry.CanGrant(
            target,
            delegatingManager,
            null,
            canDelegate: true));
    }

    private static ResourceId Permission(string path) => new(_owner, path);
}
