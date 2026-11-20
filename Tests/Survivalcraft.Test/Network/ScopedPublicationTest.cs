using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Security;

using Game.Characters;
using Game.Commands;
using Game.Components;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class ScopedPublicationTest
{
    [Fact]
    public void FurnitureCreationTravelsThroughAuthorityAndReplicatesBackToItsOwnClientWorld()
    {
        using var pair = new WorldPair();
        FurniturePackageHandler.Submit(pair.Replica, new FurnitureSetCreatedPackage { Name = "Desk" });
        Assert.Empty(pair.AuthorityFurniture.FurnitureSets);
        Assert.Empty(pair.ReplicaFurniture.FurnitureSets);
        Assert.Equal(1, pair.Client.PendingPackageCount);
        Assert.Null(pair.Authority.Self);
        pair.Flush(pair.Client);
        Assert.Equal(
            1,
            WorldNetworkBinding.For(pair.Authority.Runtime).Inbox.Drain(
                1,
                received =>
                {
                    Assert.Same(pair.Player, received.Context.Sender);
                    Assert.Same(pair.AuthorityProject, received.Context.Project);
                    new FurniturePackageHandler().Handle(
                        Assert.IsAssignableFrom<FurniturePackage>(received.Package),
                        received.Context
                    );
                }
            )
        );
        Assert.Equal("Desk", Assert.Single(pair.AuthorityFurniture.FurnitureSets).Name);
        Assert.Empty(pair.ReplicaFurniture.FurnitureSets);
        pair.Flush(pair.Authority);
        Assert.Equal(
            1,
            WorldNetworkBinding.For(pair.Client.Runtime).Inbox.Drain(
                1,
                received =>
                    new FurniturePackageHandler().Handle(
                        Assert.IsAssignableFrom<FurniturePackage>(received.Package),
                        received.Context
                    )
            )
        );
        var replica = Assert.Single(pair.ReplicaFurniture.FurnitureSets);
        Assert.Equal("Desk", replica.Name);
        Assert.NotSame(Assert.Single(pair.AuthorityFurniture.FurnitureSets), replica);
    }

    [Fact]
    public void CommandResponsesUseTheProvidedAuthorityWorldAndStopWhenThatWorldCloses()
    {
        using var pair = new WorldPair();
        pair.AuthorityProject.AddSubsystem(new SubsystemMessages());
        CommandResultPublisher.PublishRemote(
            pair.AuthorityProject,
            CommandResult.SilentOk("ready"),
            pair.Player,
            "owner"
        );
        Assert.Equal(1, pair.Authority.PendingPackageCount);
        pair.Flush(pair.Authority);
        Assert.Equal(
            1,
            WorldNetworkBinding.For(pair.Client.Runtime).Inbox.Drain(
                1,
                received =>
                {
                    var result = Assert.IsType<CommandResultPackage>(received.Package);
                    Assert.Equal("owner", result.CorrelationId);
                    Assert.Equal("ready", result.Result!.Code);
                }
            )
        );
        pair.Authority.Runtime.Close();
        CommandResultPublisher.PublishRemote(pair.AuthorityProject, CommandResult.SilentOk(), pair.Player, "closed");
        Assert.Equal(0, pair.Authority.PendingPackageCount);
        Assert.Equal(0, WorldNetworkBinding.For(pair.Client.Runtime).Inbox.Count);
    }

    [Fact]
    public void FurnitureUiCannotUseAnAuthorityProjectToBypassClientSubmission()
    {
        using var pair = new WorldPair();
        Assert.Throws<InvalidOperationException>(() => FurniturePackageHandler.Submit(
                pair.AuthorityProject,
                new FurnitureSetCreatedPackage { Name = "Wrong" }
            )
        );
        Assert.Empty(pair.AuthorityFurniture.FurnitureSets);
        Assert.Equal(0, pair.Authority.PendingPackageCount);
    }

    [Fact]
    public void EditableBlockChangesAlwaysEnterTheOwningAuthorityInbox()
    {
        using var pair = new WorldPair();
        var change = new EditableBlockValuePackage
        {
            ItemType = BuiltInBlockEditKind.Battery,
            Target = new EditableWorldBlockTarget(new CellFace(1, 64, 1, 0)),
            Value = 3
        };
        EditableBlockPackageHandler.Submit(pair.Replica, change);
        Assert.Equal(1, pair.Client.PendingPackageCount);
        Assert.Equal(0, pair.Authority.PendingPackageCount);
        pair.Flush(pair.Client);
        Assert.Equal(
            1,
            WorldNetworkBinding.For(pair.Authority.Runtime).Inbox.Drain(
                1,
                received =>
                {
                    Assert.Equal(change, received.Package);
                    Assert.Same(pair.Player, received.Context.Sender);
                    Assert.Same(pair.AuthorityProject, received.Context.Project);
                }
            )
        );
        Assert.Throws<InvalidOperationException>(() => EditableBlockPackageHandler.Submit(pair.AuthorityProject, change)
        );
    }

    [Fact]
    public void PermissionSnapshotsUseThisClientsSessionAndOnlyTrustItsCanonicalAuthority()
    {
        using var pair = new WorldPair();
        var players = new SubsystemPlayers();
        pair.Replica.AddSubsystem(players);
        pair.Replica.AddSubsystem(new SubsystemMessages());
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        var self = new Client(null, 1, pair.Player.TokenId, pair.Player.CharacterId)
        {
            PlayerAddress = address
        };
        pair.Client.AddClient(self);
        pair.Client.Self = self;
        var registry = new CommandRegistry();
        registry.Freeze();
        var handler = new CommandPackageHandler(registry);
        var permission = ResourceId.Parse("game:world.time.set");
        var snapshot = new CommandPermissionSnapshotPackage
        {
            PlayerAddress = address,
            PermissionGrants = [new CommandPermissionGrant(permission, false)]
        };
        var subject = players.Store.GetOrCreate(address);
        var foreign = new Client(null, 0, Guid.NewGuid(), Guid.Empty) { PlayerAddress = address };
        handler.Handle(
            snapshot,
            new PackageReceiveContext(pair.Client, pair.Client.Runtime, foreign)
        );
        Assert.Empty(subject.Permissions.Grants);
        NetworkSender.Send(pair.AuthorityProject, snapshot, PackageAudience.To(pair.Player));
        pair.Flush(pair.Authority);
        Assert.Equal(
            1,
            WorldNetworkBinding.For(pair.Client.Runtime).Inbox.Drain(
                1,
                received =>
                    handler.Handle(
                        Assert.IsAssignableFrom<CommandPackage>(received.Package),
                        received.Context
                    )
            )
        );
        Assert.Equal(permission, Assert.Single(subject.Permissions.Grants).Permission);
    }

    private sealed class WorldPair : IDisposable
    {
        internal RuntimeTestProject AuthorityProject { get; } = new(RuntimeSide.Authority);

        internal RuntimeTestProject Replica { get; } = new(RuntimeSide.Client);

        internal NetNode Authority => WorldNetworkBinding.For(AuthorityProject).RequireNode();

        internal NetNode Client => WorldNetworkBinding.For(Replica).RequireNode();

        internal SubsystemFurnitureBlockBehavior AuthorityFurniture { get; } = new();

        internal SubsystemFurnitureBlockBehavior ReplicaFurniture { get; } = new();

        internal Client Player { get; }

        internal WorldPair()
        {
            var authorityCharacters = new SubsystemCharacters();
            AuthorityProject.AddSubsystem(authorityCharacters);
            AuthorityProject.AddSubsystem(AuthorityFurniture);
            Replica.AddSubsystem(new SubsystemCharacters());
            Replica.AddSubsystem(ReplicaFurniture);
            Player = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
            {
                State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
            };
            var server = new Client(Guid.Empty)
                { State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live };
            Client.Server = server;
            InProcessMessageConnection.CreatePair(Authority, Player, Client, server);
            Authority.AddClient(Player);
            // Only controller metadata is needed by furniture creation, without loading a character or graphics.
            var character = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
            typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(character, AuthorityProject);
            character.CharacterId = Player.CharacterId;
            character.ComponentCharacter = new ComponentCharacter();
            ((List<CharacterData>)typeof(SubsystemCharacters).GetField(
                "_characters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(authorityCharacters)!).Add(character);
        }

        internal void Flush(NetNode node)
        {
            typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
                node,
                null
            );
        }

        public void Dispose()
        {
            Client.Runtime.Close();
            Authority.Runtime.Close();
            Replica.Dispose();
            AuthorityProject.Dispose();
        }
    }
}
