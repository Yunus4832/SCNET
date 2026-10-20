using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core;

using EntitySystem.Core;

using Game;
using Game.Commands;
using Game.Components;
using Game.Managers;
using Game.Modding;
using Game.Network;
using Game.Subsystems;

using TerritoryStoneMod;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public class TerritoryCommandTest
{
    [Fact]
    public void PlayerCommandsSupportRemoteEncodingWithoutOperatorImpersonation()
    {
        var host = new ModHost();
        host.LoadAndStart([new ModDescriptor(new ModManifest(ModEntry.ModId, "Territory", "0.1.4"), () => new ModEntry())]);
        try
        {
            IGameCommand[] commands =
            [
                new RequestTerritoryAccessCommand("Owner"),
                new DecideTerritoryAccessCommand(Guid.NewGuid(), "enter"),
                new SetTerritoryGrantCommand(Guid.NewGuid(), "none")
            ];
            foreach (var command in commands)
            {
                Assert.True(host.Commands.TryEncode(command, out var id, out var data, out _));
                Assert.True(host.Commands.TryDecode(id, data, out var decoded, out _));
                Assert.Equal(command, decoded);
                Assert.True(host.Commands.CanInvoke(command, CommandPrincipal.FromPlayer(Player(Guid.NewGuid(), "Player", null))));
                Assert.False(host.Commands.CanInvoke(command, CommandPrincipal.LocalOperator));
                Assert.False(host.Commands.CanInvoke(command, CommandPrincipal.System));
            }
        }
        finally
        {
            host.StopAll();
        }
    }

    [Fact]
    public void RequestApprovalNotificationAndRevocationUseAuthoritativePlayerIdentity()
    {
        var previous = CommonLib.WorkType;
        CommonLib.WorkType = WorkType.Local;
        try
        {
            ContentManager.Initialize();
            using var runtime = GameModRuntime.Start([
                new ModDescriptor(new ModManifest(ModEntry.ModId, "Territory", "0.1.4"), () => new ModEntry())
            ]);
            runtime.InitializeLanguage("zh-CN");
            var project = new AccessProject();
            var owner = project.AddPlayer("Owner Name");
            var visitor = project.AddPlayer("Visitor");
            var attacker = project.AddPlayer("Attacker");
            Assert.True(project.Territory.Store.TryCreate(owner.PlayerGUID, new Point3(0, 64, 0)));
            var territory = Assert.Single(project.Territory.Store.Territories);
            var adapter = new TextCommandAdapter(runtime.Commands);
            CommandResult Execute(PlayerData player, string text) => adapter.Execute(text,
                new CommandContext(CommandInvocationChannel.Text, CommandPrincipal.FromPlayer(player), project));
            CommandResult ExecuteUi(PlayerData player, IGameCommand command) => new CommandDispatcher(runtime.Commands)
                .Execute(command, new CommandContext(CommandInvocationChannel.UserInterface,
                    CommandPrincipal.FromPlayer(player), project));
            Assert.Equal("request", Assert.Single(adapter.Suggest("territory ", CommandPrincipal.FromPlayer(visitor))).Value);
            Assert.False(Execute(owner, "territory decide").Success);
            Assert.False(Execute(owner, "territory grant").Success);


            Assert.True(Execute(visitor, "territory request \"Owner Name\"").Success);
            Assert.Equal("Owner Name", Assert.Single(adapter.Suggest("territory request ",
                CommandPrincipal.FromPlayer(visitor))).Value);
            Assert.DoesNotContain(adapter.Suggest("territory request ", CommandPrincipal.FromPlayer(owner)),
                suggestion => suggestion.Value == "Owner Name");
            var request = Assert.Single(project.Territory.AccessRequests.Pending);
            Assert.False(ExecuteUi(attacker, new DecideTerritoryAccessCommand(request.Id, "enter")).Success);
            Assert.Empty(territory.Grants);
            Assert.True(ExecuteUi(owner, new DecideTerritoryAccessCommand(request.Id, "enter,dig")).Success);
            Assert.Empty(project.Territory.AccessRequests.Pending);
            Assert.Equal("territory.AlreadyGranted", Execute(visitor, "territory request \"Owner Name\"").Code);
            Assert.Empty(project.Territory.AccessRequests.Pending);
            Assert.DoesNotContain(adapter.Suggest("territory request ", CommandPrincipal.FromPlayer(visitor)),
                suggestion => suggestion.Value == "Owner Name");
            Assert.True(TerritoryProtection.CanCross(territory, visitor, false));
            Assert.True(TerritoryProtection.CanDig(territory, visitor));
            Assert.False(TerritoryProtection.CanPlace(territory, visitor));
            Assert.False(TerritoryProtection.AllowsOwner(territory, visitor));
            Assert.Contains("Owner Name", Assert.Single(project.Widgets.Messages.History).Content.PlainText);
            Assert.False(ExecuteUi(attacker, new SetTerritoryGrantCommand(visitor.PlayerGUID, "all")).Success);
            Assert.False(ExecuteUi(owner, new DecideTerritoryAccessCommand(request.Id, "enter,dig")).Success);
            Assert.True(ExecuteUi(owner, new SetTerritoryGrantCommand(visitor.PlayerGUID, "none")).Success);
            Assert.False(TerritoryProtection.CanCross(territory, visitor, false));
            Assert.False(TerritoryProtection.CanDig(territory, visitor));
            Assert.True(TerritoryProtection.CanCross(territory, visitor, true));
            Assert.Empty(territory.Grants);

            Assert.True(Execute(attacker, "territory request \"Owner Name\"").Success);
            var rejected = Assert.Single(project.Territory.AccessRequests.Pending);
            Assert.True(ExecuteUi(owner, new DecideTerritoryAccessCommand(rejected.Id, "none")).Success);
            Assert.Empty(project.Territory.AccessRequests.Pending);
            Assert.Empty(territory.Grants);
            Assert.Contains("拒绝", project.Widgets.Messages.History.Last().Content.PlainText);
            Assert.Contains("联系领主", project.Widgets.Messages.History.Last().Content.PlainText);
            Assert.Contains("重新申请", project.Widgets.Messages.History.Last().Content.PlainText);
            Assert.Equal("territory.RateLimited", Execute(attacker, "territory request \"Owner Name\"").Code);

            owner.ComponentPlayer = null;
            Assert.Equal("territory.OwnerOffline", Execute(attacker,
                $"territory request {owner.PlayerGUID}").Code);
        }
        finally
        {
            CommonLib.WorkType = previous;
        }
    }

    private sealed class AccessProject : Project
    {
        public TerritorySubsystem Territory { get; } = new();
        public SubsystemPlayers Players { get; } = new();
        public SubsystemGameWidgets Widgets { get; } = new();

        public AccessProject()
        {
            foreach (var subsystem in new Subsystem[] { Territory, Players, Widgets })
            {
                subsystems.Add(subsystem);
                typeof(Subsystem).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(subsystem, this);
                typeof(Subsystem).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(subsystem, true);
            }
        }

        public PlayerData AddPlayer(string name)
        {
            var player = Player(Guid.NewGuid(), name, this);
            var players = (List<PlayerData>)typeof(SubsystemPlayers)
                .GetField("_playersData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Players)!;
            players.Add(player);
            return player;
        }
    }

    private static PlayerData Player(Guid id, string name, Project? project)
    {
        var player = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
        player.PlayerGUID = id;
        player.Name = name;
        player.ComponentPlayer = (ComponentPlayer)RuntimeHelpers.GetUninitializedObject(typeof(ComponentPlayer));
        typeof(PlayerData).GetProperty(nameof(PlayerData.IsMainPlayer))!.SetValue(player, true);
        typeof(PlayerData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, project);
        typeof(PlayerData).GetField("<CommandPermissions>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, new CommandPermissionSet());
        return player;
    }
}
