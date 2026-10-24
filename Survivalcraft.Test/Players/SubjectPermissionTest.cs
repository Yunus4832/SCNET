using System.Runtime.CompilerServices;

using Engine.Core.Security;

using EntitySystem.Core;

using Game;
using Game.Commands;
using Game.Protocol.Identity;
using Game.Subsystems;

namespace Game.Test.Players;

public sealed class SubjectPermissionTest
{
    [Fact]
    public void ControlledCharacterDoesNotSupplyItsOwnersPermissions()
    {
        var players = new SubsystemPlayers();
        var owner = players.Store.GetOrCreate(new PlayerAddress(IdentityAddress.Encode(new byte[32])));
        var actor = players.Store.GetOrCreate(
            new PlayerAddress(IdentityAddress.Encode(Enumerable.Repeat((byte)1, 32).ToArray()))
        );
        var ownersPermission = ResourceId.Parse("game:owner.only");
        var actorsPermission = ResourceId.Parse("game:actor.only");
        owner.Permissions.Grant(ownersPermission, true);
        actor.Permissions.Grant(actorsPermission, false);
        var character = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        character.SubsystemCharacters = new SubsystemCharacters();
        character.OwnerAddress = owner.Address;
        character.Name = "Character";

        var principal = CommandPrincipal.FromSubject(actor, character);
        Assert.Same(actor, principal.Subject);
        Assert.Same(character, principal.Character);
        Assert.True(principal.HasPermission(actorsPermission));
        Assert.False(principal.HasPermission(ownersPermission));
        Assert.False(principal.CanDelegate(ownersPermission));
    }

    [Fact]
    public void SubjectWithoutACharacterStillHasItsPermissions()
    {
        var players = new SubsystemPlayers();
        var user = players.Store.GetOrCreate(new PlayerAddress(IdentityAddress.Encode(new byte[32])));
        user.Permissions.Grant(BuiltInPermissionIds.ManageStandard, true);
        var principal = CommandPrincipal.FromSubject(user);
        Assert.Null(principal.Character);
        Assert.Same(user, principal.Subject);
        Assert.True(principal.CanDelegate(BuiltInPermissionIds.ManageStandard));
    }

    [Fact]
    public void OfflineAdministratorOrRevocationDoesNotReopenPublicClaim()
    {
        var players = new SubsystemPlayers();
        var user = players.Store.GetOrCreate(new PlayerAddress(IdentityAddress.Encode(new byte[32])));
        var info = new SubsystemGameInfo();
        using var project = new ClaimProject(players, info);
        Assert.False(ServerAdministrationBootstrap.IsClaimed(project));
        user.Permissions.Grant(BuiltInPermissionIds.ManageStandard, true);
        Assert.True(ServerAdministrationBootstrap.IsClaimed(project));
        Assert.Single(players.Store.Entries);
        user.Permissions.Revoke(BuiltInPermissionIds.ManageStandard);
        Assert.True(ServerAdministrationBootstrap.IsClaimed(project));
        Assert.True(info.ServerAdministrationClaimed);
    }

    private sealed class ClaimProject : Project
    {
        internal ClaimProject(SubsystemPlayers players, SubsystemGameInfo info)
        {
            subsystems.Add(players);
            subsystems.Add(info);
        }
    }
}
