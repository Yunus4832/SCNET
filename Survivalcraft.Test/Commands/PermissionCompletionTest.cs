using Game;
using Game.Commands;
using Game.Localization;
using Game.Modding;
using Game.Network;
using Game.Network.Enums;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Commands;

[Collection(NetworkWorkTypeCollection.Name)]
public class PermissionCompletionTest
{
    [Theory]
    [InlineData(WorkType.Client)]
    [InlineData(WorkType.Server)]
    public void GrantAndDelegationUseTheirOwnPoliciesAndPermissionDescriptions(WorkType workType)
    {
        var previous = CommonLib.WorkType;
        try
        {
            CommonLib.WorkType = workType;
            var registry = new CommandRegistry();
            BuiltInCommands.Register(registry, new ModId("game"));
            registry.Freeze();
            var adapter = new TextCommandAdapter(registry);
            var principal = new CommandPrincipal("Operator", CommandPrincipalKind.ServerOperator);
            var grants = adapter.Suggest("/permission grant Tester ", principal);
            var others = Assert.Single(grants, item => item.Value == "game:player.teleport.others");
            var self = Assert.Single(grants, item => item.Value == "game:player.teleport.self");
            Assert.Equal("TeleportOther_Description", others.DescriptionSource!.Key);
            Assert.Equal("TeleportSelf_Description", self.DescriptionSource!.Key);
            Assert.All(grants, item => Assert.NotEqual(LocalizedText.Empty, item.DescriptionSource));
            Assert.Contains(adapter.Suggest("/permission delegate Tester ", principal),
                item => item.Value == "game:player.teleport.others");
            Assert.Contains(adapter.Suggest("/permission delegate Tester ", principal),
                item => item.Value == "game:player.teleport.self");
            Assert.DoesNotContain(grants, item => item.Value == "game:server.stop");
            var manager = new CommandPrincipal("Manager", permissions: [BuiltInPermissionIds.ManageStandard]);
            Assert.Contains(adapter.Suggest("/permission grant Tester ", manager),
                item => item.Value == "game:player.teleport.others");
            var permission = new ResourceId(new ModId("game"), "player.teleport.others");
            var delegated = new CommandPrincipal("Delegate", delegablePermissions: [permission]);
            Assert.Contains(adapter.Suggest("/permission delegate Tester ", delegated),
                item => item.Value == "game:player.teleport.others");
            var user = new CommandPrincipal("User", permissions: [permission]);
            Assert.True(registry.CanInvoke(typeof(TeleportPlayerCommand), user));
            Assert.DoesNotContain(adapter.Suggest("/permission delegate Tester ", user),
                item => item.Value == "game:player.teleport.others");
        }
        finally
        {
            CommonLib.WorkType = previous;
        }
    }
}
