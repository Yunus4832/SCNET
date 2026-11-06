using Game.Commands;
using Game.Commands.BuiltIn;
using Game.Commands.Text;
using Game.Network;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Commands;

[Collection(NodeRoleCollection.Name)]
public class PermissionCompletionTest
{
    [Theory]
    [InlineData(NodeRole.Client)]
    [InlineData(NodeRole.Authority)]
    public void GrantAndDelegationUseTheirOwnPoliciesAndPermissionDescriptions(NodeRole worldRole)
    {
        var previous = CommonLib.Net.Role;
        try
        {
            CommonLib.Net.Role = worldRole;
            var registry = new CommandRegistry();
            Fixtures.BuiltInCommandTestRegistry.Register(registry, new ModId("game"));
            registry.Freeze();
            var adapter = new TextCommandAdapter(registry, Game.Managers.GameManager.AuthorityProject);
            var principal = CommandPrincipal.LocalOperator;
            var grants = adapter.Suggest("/permission grant Tester ", principal);
            var others = Assert.Single(grants, item => item.Value == "game:player.teleport.others");
            var self = Assert.Single(grants, item => item.Value == "game:player.teleport.self");
            Assert.Equal("TeleportOther_Description", others.DescriptionSource!.Key);
            Assert.Equal("TeleportSelf_Description", self.DescriptionSource!.Key);
            Assert.All(grants, item => Assert.NotEqual(LocalizedText.Empty, item.DescriptionSource));
            Assert.Contains(
                adapter.Suggest("/permission delegate Tester ", principal),
                item => item.Value == "game:player.teleport.others"
            );
            Assert.Contains(
                adapter.Suggest("/permission delegate Tester ", principal),
                item => item.Value == "game:player.teleport.self"
            );
            Assert.DoesNotContain(grants, item => item.Value == "game:server.stop");
            var manager = CommandPrincipal.CreatePlayer("Manager", permissions: [BuiltInPermissionIds.ManageStandard]);
            Assert.Contains(
                adapter.Suggest("/permission grant Tester ", manager),
                item => item.Value == "game:player.teleport.others"
            );
            var permission = new ResourceId(new ModId("game"), "player.teleport.others");
            var delegated = CommandPrincipal.CreatePlayer("Delegate", delegablePermissions: [permission]);
            Assert.Contains(
                adapter.Suggest("/permission delegate Tester ", delegated),
                item => item.Value == "game:player.teleport.others"
            );
            var user = CommandPrincipal.CreatePlayer("User", permissions: [permission]);
            Assert.True(
                registry.CanInvoke(typeof(TeleportPlayerCommand), user, Game.Managers.GameManager.AuthorityProject)
            );
            Assert.DoesNotContain(
                adapter.Suggest("/permission delegate Tester ", user),
                item => item.Value == "game:player.teleport.others"
            );
        }
        finally
        {
            CommonLib.Net.Role = previous;
        }
    }
}
