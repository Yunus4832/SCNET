using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using Game.Blocks;
using Game.Characters;
using Game.Components;
using Game.Furniture;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class FurnitureDesignRenameTest
{
    [Fact]
    public void ConfirmedRenameTravelsThroughAuthorityAndConvergesOnBothClientDesignTables()
    {
        using var pair = new RenameWorlds();
        var package = new FurnitureDesignRenameRequestPackage
            { Source = pair.Source, FurnitureIndex = 7, Name = "Renamed" };
        var restored =
            Assert.IsType<FurnitureDesignRenameRequestPackage>(
                ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(package))
            );
        FurniturePackageHandler.Submit(pair.First, restored);
        Assert.Equal("Original", pair.FirstFurniture.GetDesign(7)!.Name);
        Assert.Equal("Original", pair.AuthorityFurniture.GetDesign(7)!.Name);
        pair.DeliverRequest();
        Assert.Equal("Renamed", pair.AuthorityFurniture.GetDesign(7)!.Name);
        Assert.Equal("Original", pair.SecondFurniture.GetDesign(7)!.Name);
        pair.DeliverResults();
        Assert.Equal("Renamed", pair.FirstFurniture.GetDesign(7)!.Name);
        Assert.Equal("Renamed", pair.SecondFurniture.GetDesign(7)!.Name);
        Assert.NotSame(pair.AuthorityFurniture.GetDesign(7), pair.FirstFurniture.GetDesign(7));
    }

    [Theory]
    [InlineData(8, "Renamed")]
    [InlineData(7, " Bad")]
    [InlineData(7, "Bad!")]
    public void ObsoleteIndexAndInvalidNamesLeaveTheAuthorityDesignUnchanged(int index, string name)
    {
        using var pair = new RenameWorlds();
        new FurniturePackageHandler().Handle(
            new FurnitureDesignRenameRequestPackage
            {
                Source = pair.Source, FurnitureIndex = index, Name = name
            },
            new PackageReceiveContext(pair.AuthorityNode, pair.AuthorityNode.Runtime, pair.FirstConnection)
        );
        Assert.Equal("Original", pair.AuthorityFurniture.GetDesign(7)!.Name);
        Assert.Equal(0, pair.AuthorityNode.PendingPackageCount);
    }

    [Fact]
    public void WrongDirectionForeignWorldAndRemovedSourceCannotRenameDesigns()
    {
        using var pair = new RenameWorlds();
        var package = new FurnitureDesignRenameRequestPackage
            { Source = pair.Source, FurnitureIndex = 7, Name = "Renamed" };
        new FurniturePackageHandler().Handle(
            package,
            new PackageReceiveContext(pair.FirstNode, pair.FirstNode.Runtime, pair.FirstServer)
        );
        Assert.Equal("Original", pair.FirstFurniture.GetDesign(7)!.Name);
        Assert.False(FurniturePackageHandler.ApplyDesignRename(pair.Authority, pair.FirstServer, package));
        pair.AuthorityTerrain.Terrain.SetCellValueFast(pair.Source.X, pair.Source.Y, pair.Source.Z, 0);
        Assert.False(FurniturePackageHandler.ApplyDesignRename(pair.Authority, pair.FirstConnection, package));
        Assert.Equal("Original", pair.AuthorityFurniture.GetDesign(7)!.Name);
    }

    [Fact]
    public void CancellingDetachedSourceEditsSendsNothingAndChangesNoWorld()
    {
        using var pair = new RenameWorlds();
        var copy = SubsystemFurniturePresentation.CopySourceDesign(pair.FirstFurniture.GetDesign(7))!;
        copy.Name = "Cancelled";
        Assert.Equal("Original", pair.FirstFurniture.GetDesign(7)!.Name);
        Assert.Equal("Original", pair.AuthorityFurniture.GetDesign(7)!.Name);
        Assert.Equal(0, pair.FirstNode.PendingPackageCount);
    }

    private sealed class RenameWorlds : IDisposable
    {
        private readonly WorldCatalogScope _authority;

        private readonly WorldCatalogScope _first;

        private readonly WorldCatalogScope _second;

        private readonly List<IDisposable> _connections = [];

        internal NetNode AuthorityNode { get; } = new(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );

        internal NetNode FirstNode { get; } = new(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );

        internal NetNode SecondNode { get; } = new(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );

        internal RuntimeTestProject Authority => _authority.Project;

        internal RuntimeTestProject First => _first.Project;

        internal RuntimeTestProject Second => _second.Project;

        internal SubsystemFurnitureBlockBehavior AuthorityFurniture { get; } = new();

        internal SubsystemFurnitureBlockBehavior FirstFurniture { get; } = new();

        internal SubsystemFurnitureBlockBehavior SecondFurniture { get; } = new();

        internal SubsystemTerrain AuthorityTerrain { get; } = new() { Terrain = new Terrain() };

        internal Client FirstConnection { get; }

        internal Client FirstServer { get; }

        internal CellFace Source { get; } = new(1, 65, 1, 4);

        internal RenameWorlds()
        {
            _authority = new WorldCatalogScope(AuthorityNode);
            _first = new WorldCatalogScope(FirstNode);
            _second = new WorldCatalogScope(SecondNode);
            Setup(Authority, AuthorityFurniture, AuthorityTerrain, _authority.Catalog);
            Setup(First, FirstFurniture, new SubsystemTerrain { Terrain = new Terrain() }, _first.Catalog);
            Setup(Second, SecondFurniture, new SubsystemTerrain { Terrain = new Terrain() }, _second.Catalog);
            var first = Connect(FirstNode, 1);
            FirstConnection = first.Item1;
            FirstServer = first.Item2;
            _ = Connect(SecondNode, 2);
            var character = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
            typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(character, Authority);
            character.CharacterId = FirstConnection.CharacterId;
            character.ComponentCharacter = new ComponentCharacter
            {
                CharacterData = character, ComponentBody = new ComponentBody { Position = new Vector3(1, 65, 1) }
            };
            ((List<CharacterData>)typeof(SubsystemCharacters).GetField(
                    "_characters",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )!
                .GetValue(Authority.FindSubsystem<SubsystemCharacters>(true)!)!).Add(character);
            var interest = new SubsystemNetworkInterest();
            Authority.AddSubsystem(interest);
            // Preserve real interest authorization: use the actual roster/body location, with no running terrain worker.
            var updater = (TerrainUpdater)RuntimeHelpers.GetUninitializedObject(typeof(TerrainUpdater));
            var parameters = typeof(TerrainUpdater).GetField(
                "_updateParameters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;
            var state = Activator.CreateInstance(parameters.FieldType, true)!;
            state.GetType().GetField("Locations")!.SetValue(state, new Dictionary<int, TerrainUpdateLocation>());
            parameters.SetValue(updater, state);
            typeof(SubsystemNetworkInterest).GetField(
                    "_subsystemTerrain",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )!
                .SetValue(interest, new SubsystemTerrain { TerrainUpdater = updater });
            typeof(SubsystemNetworkInterest).GetField(
                    "_subsystemCharacters",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )!
                .SetValue(interest, Authority.FindSubsystem<SubsystemCharacters>(true)!);
            AuthorityTerrain.Terrain.SetCellValueFast(
                Source.X,
                Source.Y,
                Source.Z,
                Terrain.MakeBlockValue(
                    BuiltInBlockRegistry.GetIndex<FurnitureBlock>(),
                    0,
                    FurnitureBlock.SetDesignIndex(0, 7, 0, false)
                )
            );
        }

        private (Client, Client) Connect(NetNode node, byte id)
        {
            var connection = new Client(null, id, Guid.NewGuid(), Guid.NewGuid())
                { State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live };
            var server = new Client(Guid.Empty) { State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live };
            node.Server = server;
            AuthorityNode.AddClient(connection);
            var pair = InProcessMessageConnection.CreatePair(AuthorityNode, connection, node, server);
            _connections.Add(pair.ToAuthority);
            _connections.Add(pair.ToClient);
            return (connection, server);
        }

        private static void Setup(
            RuntimeTestProject project,
            SubsystemFurnitureBlockBehavior furniture,
            SubsystemTerrain terrain,
            BlockContentCatalog catalog
        )
        {
            terrain.Terrain.AllocateChunk(0, 0);
            project.AddSubsystem(terrain);
            project.AddSubsystem(new SubsystemCharacters());
            project.AddSubsystem(furniture);
            var design = new FurnitureDesign(catalog, terrain) { Index = 7, Name = "Original" };
            design.SetValues(2, new int[8]);
            furniture.InstallNetworkDesign(design);
        }

        internal void DeliverRequest()
        {
            Flush(FirstNode);
            Assert.Equal(
                1,
                WorldNetworkBinding.For(Authority).Inbox.Drain(
                    10,
                    received =>
                        new FurniturePackageHandler().Handle(
                            Assert.IsAssignableFrom<FurniturePackage>(received.Package),
                            received.Context
                        )
                )
            );
        }

        internal void DeliverResults()
        {
            Flush(AuthorityNode);
            foreach (var project in new[] { First, Second })
            {
                Assert.Equal(
                    1,
                    WorldNetworkBinding.For(project).Inbox.Drain(
                        10,
                        received =>
                            new FurniturePackageHandler().Handle(
                                Assert.IsAssignableFrom<FurniturePackage>(received.Package),
                                received.Context
                            )
                    )
                );
            }
        }

        private static void Flush(NetNode node)
        {
            typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
                node,
                null
            );
        }

        public void Dispose()
        {
            foreach (var connection in _connections) { connection.Dispose(); }

            _second.Dispose();
            _first.Dispose();
            _authority.Dispose();
        }
    }
}
