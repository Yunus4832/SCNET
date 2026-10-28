using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;
using Engine.Core.Security;

using EntitySystem.Core;

using Game.Characters;
using Game.Commands;
using Game.Components;
using Game.Managers;
using Game.Modding;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.World;

using TerritoryStoneMod;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public class TerritoryCommandTest : IDisposable
{
    private readonly Dictionary<int, Client> _previousClients = CommonLib.Net.Clients;

    private readonly Client? _previousSelf = CommonLib.Net.Self;

    public TerritoryCommandTest()
    {
        CommonLib.Net.Clients = [];
    }

    public void Dispose()
    {
        CommonLib.Net.Clients = _previousClients;
        CommonLib.Net.Self = _previousSelf;
    }

    [Fact]
    public void PlayerCommandsSupportRemoteEncodingWithoutOperatorImpersonation()
    {
        var host = new ModHost();
        host.LoadAndStart(
            [
                new ModDescriptor(new ModManifest(ModEntry.ModId, "Territory", "0.1.4"), () => new ModEntry())
            ]
        );
        try
        {
            IGameCommand[] commands =
            [
                new RequestTerritoryAccessCommand("Owner"),
                new DecideTerritoryAccessCommand(Guid.NewGuid(), "enter"),
                new SetTerritoryGrantCommand(TerritoryTestIdentity.Address(), "none")
            ];
            foreach (var command in commands)
            {
                Assert.True(host.Commands.TryEncode(command, out var id, out var data, out _));
                Assert.True(host.Commands.TryDecode(id, data, out var decoded, out _));
                Assert.Equal(command, decoded);
                Assert.True(
                    host.Commands.CanInvoke(
                        command,
                        CommandPrincipal.CreatePlayer("Player")
                    )
                );
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
        var previous = GameManager.Role;
        CommonLib.Net.Role = WorldRole.SinglePlayer;
        try
        {
            ContentManager.Initialize();
            using var runtime = GameModRuntime.Start(
                [
                    new ModDescriptor(new ModManifest(ModEntry.ModId, "Territory", "0.1.4"), () => new ModEntry())
                ]
            );
            runtime.InitializeLanguage("zh-CN");
            var project = new AccessProject();
            var owner = project.AddPlayer("Owner Name");
            var visitor = project.AddPlayer("Visitor");
            var attacker = project.AddPlayer("Attacker");
            CommonLib.Net.Self = visitor.Client;
            Assert.True(project.Territory.Store.TryCreate(owner.OwnerAddress, new Point3(0, 64, 0)));
            var territory = Assert.Single(project.Territory.Store.Territories);
            var adapter = new TextCommandAdapter(runtime.Commands);

            CommandResult Execute(CharacterData player, string text) => adapter.Execute(
                text,
                new CommandContext(
                    CommandInvocationChannel.Text,
                    CommandPrincipal.FromSubject(
                        player.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(player.OwnerAddress),
                        player
                    ),
                    project
                )
            );

            CommandResult ExecuteUi(CharacterData player, IGameCommand command) =>
                new CommandDispatcher(runtime.Commands)
                    .Execute(
                        command,
                        new CommandContext(
                            CommandInvocationChannel.UserInterface,
                            CommandPrincipal.FromSubject(
                                player.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(
                                    player.OwnerAddress
                                ),
                                player
                            ),
                            project
                        )
                    );

            Assert.Equal(
                "request",
                Assert.Single(
                    adapter.Suggest(
                        "territory ",
                        CommandPrincipal.FromSubject(
                            visitor.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(
                                visitor.OwnerAddress
                            ),
                            visitor
                        )
                    )
                ).Value
            );
            Assert.False(Execute(owner, "territory decide").Success);
            Assert.False(Execute(owner, "territory grant").Success);


            Assert.True(Execute(visitor, $"territory request {owner.OwnerAddress.Value}").Success);
            Assert.Equal(
                owner.OwnerAddress.Value,
                Assert.Single(
                    adapter.Suggest(
                        "territory request ",
                        CommandPrincipal.FromSubject(
                            visitor.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(
                                visitor.OwnerAddress
                            ),
                            visitor
                        )
                    )
                ).Value
            );
            Assert.DoesNotContain(
                adapter.Suggest(
                    "territory request ",
                    CommandPrincipal.FromSubject(
                        owner.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(owner.OwnerAddress),
                        owner
                    )
                ),
                suggestion => suggestion.Value == owner.OwnerAddress.Value
            );
            var request = Assert.Single(project.Territory.AccessRequests.Pending);
            Assert.False(ExecuteUi(attacker, new DecideTerritoryAccessCommand(request.Id, "enter")).Success);
            Assert.Empty(territory.Grants);
            Assert.True(ExecuteUi(owner, new DecideTerritoryAccessCommand(request.Id, "enter,dig")).Success);
            Assert.Empty(project.Territory.AccessRequests.Pending);
            Assert.Equal(
                "territory.AlreadyGranted",
                Execute(visitor, $"territory request {owner.OwnerAddress.Value}").Code
            );
            Assert.Empty(project.Territory.AccessRequests.Pending);
            Assert.DoesNotContain(
                adapter.Suggest(
                    "territory request ",
                    CommandPrincipal.FromSubject(
                        visitor.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(visitor.OwnerAddress),
                        visitor
                    )
                ),
                suggestion => suggestion.Value == owner.OwnerAddress.Value
            );
            Assert.True(TerritoryProtection.CanCross(territory, visitor, false));
            Assert.True(TerritoryProtection.CanDig(territory, visitor));
            Assert.False(TerritoryProtection.CanPlace(territory, visitor));
            Assert.False(TerritoryProtection.AllowsOwner(territory, visitor));
            Assert.Contains(
                project.Territory.OwnerName(owner.OwnerAddress),
                Assert.Single(project.Widgets.Messages.History).Content.PlainText
            );
            Assert.False(ExecuteUi(attacker, new SetTerritoryGrantCommand(visitor.OwnerAddress, "all")).Success);
            Assert.False(ExecuteUi(owner, new DecideTerritoryAccessCommand(request.Id, "enter,dig")).Success);
            Assert.True(ExecuteUi(owner, new SetTerritoryGrantCommand(visitor.OwnerAddress, "none")).Success);
            Assert.False(TerritoryProtection.CanCross(territory, visitor, false));
            Assert.False(TerritoryProtection.CanDig(territory, visitor));
            Assert.True(TerritoryProtection.CanCross(territory, visitor, true));
            Assert.Empty(territory.Grants);

            CommonLib.Net.Self = attacker.Client;
            Assert.True(Execute(attacker, $"territory request {owner.OwnerAddress.Value}").Success);
            var rejected = Assert.Single(project.Territory.AccessRequests.Pending);
            Assert.True(ExecuteUi(owner, new DecideTerritoryAccessCommand(rejected.Id, "none")).Success);
            Assert.Empty(project.Territory.AccessRequests.Pending);
            Assert.Empty(territory.Grants);
            Assert.Contains("拒绝", project.Widgets.Messages.History.Last().Content.PlainText);
            Assert.Contains("联系领主", project.Widgets.Messages.History.Last().Content.PlainText);
            Assert.Contains("重新申请", project.Widgets.Messages.History.Last().Content.PlainText);
            Assert.Equal(
                "territory.RateLimited",
                Execute(attacker, $"territory request {owner.OwnerAddress.Value}").Code
            );

            owner.ComponentCharacter = null;
            CommonLib.Net.Clients.Remove(owner.Client!.ID);
            Assert.Equal(
                "territory.OwnerOffline",
                Execute(
                    attacker,
                    $"territory request {owner.OwnerAddress.Value}"
                ).Code
            );
        }
        finally
        {
            CommonLib.Net.Role = previous;
        }
    }

    [Fact]
    public void CharacterlessSubjectsCanRequestApproveAndRevokeAccess()
    {
        var previous = GameManager.Role;
        CommonLib.Net.Role = WorldRole.SinglePlayer;
        try
        {
            ContentManager.Initialize();
            using var runtime = GameModRuntime.Start(
                [new ModDescriptor(new ModManifest(ModEntry.ModId, "Territory", "0.1.4"), () => new ModEntry())]
            );
            runtime.InitializeLanguage("zh-CN");
            using var project = new AccessProject();
            var owner = project.Players.Store.GetOrCreate(TerritoryTestIdentity.Address());
            var visitor = project.Players.Store.GetOrCreate(TerritoryTestIdentity.Address());
            var ownerSession = new Client(null, 1, Guid.NewGuid(), Guid.Empty, project)
                { PlayerAddress = owner.Address };
            var visitorSession = new Client(null, 2, Guid.NewGuid(), Guid.Empty, project)
                { PlayerAddress = visitor.Address };
            CommonLib.Net.Clients = new Dictionary<int, Client> { [1] = ownerSession, [2] = visitorSession };
            CommonLib.Net.Self = visitorSession;
            Assert.True(project.Territory.Store.TryCreate(owner.Address, new Point3(0, 64, 0)));
            var dispatcher = new CommandDispatcher(runtime.Commands);

            CommandResult Execute(Client session, IGameCommand command) => dispatcher.Execute(
                command,
                new CommandContext(CommandInvocationChannel.Network, CommandPrincipal.FromSession(session), project)
            );

            Assert.True(Execute(visitorSession, new RequestTerritoryAccessCommand(owner.Address.Value)).Success);
            var request = Assert.Single(project.Territory.AccessRequests.Pending);
            Assert.Equal(visitor.Address, request.User);
            Assert.False(Execute(visitorSession, new DecideTerritoryAccessCommand(request.Id, "dig")).Success);
            Assert.True(Execute(ownerSession, new DecideTerritoryAccessCommand(request.Id, "dig")).Success);
            var territory = Assert.Single(project.Territory.Store.Territories);
            Assert.Equal(TerritoryPermission.Dig, territory.PermissionsFor(visitor.Address));
            Assert.True(Execute(ownerSession, new SetTerritoryGrantCommand(visitor.Address, "none")).Success);
            Assert.Empty(territory.Grants);
            Assert.Null(ownerSession.CharacterData);
            Assert.Null(visitorSession.CharacterData);
        }
        finally { CommonLib.Net.Role = previous; }
    }

    private sealed class AccessProject : Project
    {
        public TerritorySubsystem Territory { get; } = new();

        public SubsystemPlayers Players { get; } = new();

        public SubsystemCharacters Characters { get; } = new();

        public SubsystemPositionMarks Marks { get; } = new();

        public SubsystemGameWidgets Widgets { get; } = new();

        public AccessProject()
        {
            foreach (var subsystem in new Subsystem[] { Territory, Players, Characters, Marks, Widgets })
            {
                subsystems.Add(subsystem);
                typeof(Subsystem).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(subsystem, this);
                typeof(Subsystem).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(subsystem, true);
            }
        }

        public CharacterData AddPlayer(string name)
        {
            var player = Player(Guid.NewGuid(), name, this);
            player.SubsystemCharacters = Characters;
            Players.Store.GetOrCreate(player.OwnerAddress).DisplayName = name;
            var id = (byte)(CommonLib.Net.Clients.Count + 1);
            CommonLib.Net.Clients[id] = new Client(null, id, Guid.NewGuid(), player.CharacterId, this)
                { PlayerAddress = player.OwnerAddress };
            var players = (List<CharacterData>)typeof(SubsystemCharacters)
                .GetField("_characters", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Characters)!;
            players.Add(player);
            return player;
        }
    }

    private static CharacterData Player(Guid id, string name, Project? project)
    {
        var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        player.CharacterId = id;
        player.Name = name;
        player.ComponentCharacter =
            (ComponentCharacter)RuntimeHelpers.GetUninitializedObject(typeof(ComponentCharacter));
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(player, true);
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, project);
        player.SubsystemCharacters = new SubsystemCharacters();
        var bytes = System.Security.Cryptography.SHA256.HashData(id.ToByteArray());
        player.OwnerAddress =
            new PlayerAddress(IdentityAddress.Encode(bytes));

        return player;
    }
}
