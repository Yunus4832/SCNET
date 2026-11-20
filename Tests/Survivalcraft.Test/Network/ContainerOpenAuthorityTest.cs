using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Characters;
using Game.Components;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Replication;
using Game.Network.Transport;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class ContainerOpenAuthorityTest
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void AdventureDispenserRejectsBothEntryPointsBeforeRegisteringObservers(bool byPoint, bool wireRoundTrip)
    {
        using var fixture = new Fixture(new ComponentDispenser(), GameMode.Adventure);
        fixture.Request(byPoint, wireRoundTrip);
        Assert.Empty(fixture.Pending);
        Assert.Empty(
            fixture.Interest.Entities.GetObservers(EntityInterestGroup.BlockEntities, fixture.Inventory.Entity.EntityId)
        );
    }

    [Theory]
    [InlineData("chest", true)]
    [InlineData("chest", false)]
    [InlineData("crafting", true)]
    [InlineData("crafting", false)]
    [InlineData("furnace", true)]
    [InlineData("furnace", false)]
    [InlineData("dispenser", true)]
    [InlineData("dispenser", false)]
    public void AuthorizedLocalAndWireRequestsKeepTheSameInventoryIdAndBaselineOrder(string kind, bool byPoint)
    {
        ComponentInventoryBase Create() => kind switch
        {
            "chest" => new ComponentChest(), "crafting" => new ComponentCraftingTable(),
            "furnace" => new ComponentFurnace(), _ => new ComponentDispenser()
        };

        foreach (var wire in new[] { false, true })
        {
            using var fixture = new Fixture(Create(), GameMode.Survival);
            fixture.Request(byPoint, wire);
            var packets = fixture.Pending.Select(message => message.Package).ToArray();
            Assert.IsType<SingleEntitySnapshotPackage>(packets[0]);
            var reply = Assert.IsType<OpenInventoryPackage>(packets[1]);
            Assert.Equal(7, reply.InventoryId);
            Assert.All(fixture.Pending, message => Assert.Same(fixture.Actor, message.Audience.SingleRecipient));
            Assert.Same(
                fixture.Actor,
                Assert.Single(
                    fixture.Interest.Entities.GetObservers(
                        EntityInterestGroup.BlockEntities,
                        fixture.Inventory.Entity.EntityId
                    )
                )
            );
        }
    }

    [Theory]
    [InlineData("chest")]
    [InlineData("crafting")]
    [InlineData("furnace")]
    public void AdventureKeepsTheOtherContainerPolicies(string kind)
    {
        ComponentInventoryBase inventory = kind switch
        {
            "chest" => new ComponentChest(), "crafting" => new ComponentCraftingTable(), _ => new ComponentFurnace()
        };
        using var fixture = new Fixture(inventory, GameMode.Adventure);
        fixture.Request(true, false);
        Assert.Contains(fixture.Pending, message => message.Package is OpenInventoryPackage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OutOfRangeRequestsProduceNeitherBaselineNorOpenReply(bool byPoint)
    {
        using var fixture = new Fixture(new ComponentChest(), GameMode.Survival);
        fixture.Character.ComponentCharacter!.ComponentBody.Position = new Vector3(1000, 64, 1000);
        fixture.Request(byPoint, false);
        Assert.Empty(fixture.Pending);
        Assert.Empty(
            fixture.Interest.Entities.GetObservers(EntityInterestGroup.BlockEntities, fixture.Inventory.Entity.EntityId)
        );
    }

    [Theory]
    [InlineData(typeof(SubsystemChestBlockBehavior))]
    [InlineData(typeof(SubsystemCraftingTableBlockBehavior))]
    [InlineData(typeof(SubsystemFurnaceBlockBehavior))]
    [InlineData(typeof(SubsystemDispenserBlockBehavior))]
    public void LocalClientUsesThePositionRequestWithoutCreatingGuiOrSound(Type behaviorType)
    {
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var authorityNode = WorldNetworkBinding.For(authority).RequireNode();
        var clientNode = WorldNetworkBinding.For(client).RequireNode();
        var actor = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        var server = new Client(Guid.Empty);
        var pair = InProcessMessageConnection.CreatePair(authorityNode, actor, clientNode, server);
        using var toClient = pair.ToClient;
        using var toAuthority = pair.ToAuthority;
        clientNode.Server = server;
        var behavior = (SubsystemBlockBehavior)Activator.CreateInstance(behaviorType)!;
        client.InitializeSubsystem(behavior);
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(data, true);
        var character = new ComponentCharacter { CharacterData = data };
        client.InitializeComponent(character);
        var miner = new ComponentMiner();
        client.InitializeComponent(miner);
        typeof(ComponentMiner).GetProperty(nameof(ComponentMiner.ComponentCharacter))!.SetValue(miner, character);
        var point = new Point3(1, 64, 1);
        Assert.True(
            behavior.OnInteract(
                new TerrainRaycastResult { CellFace = new CellFace(point.X, point.Y, point.Z, 0) },
                miner
            )
        );
        var pending = (PendingLogicalMessages)typeof(NetNode).GetField(
            "_pendingPackages",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!.GetValue(clientNode)!;
        Assert.Equal(point, Assert.IsType<OpenBlockInventoryPackage>(Assert.Single(pending).Package).Point3);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly RuntimeTestProject _authority = new(RuntimeSide.Authority);

        private readonly RuntimeTestProject _client = new(RuntimeSide.Client);

        private readonly InProcessMessageConnection _toClient;

        private readonly InProcessMessageConnection _toAuthority;

        private readonly Point3 _point = new(1, 64, 1);

        public Fixture(ComponentInventoryBase inventory, GameMode mode)
        {
            Inventory = inventory;
            var node = WorldNetworkBinding.For(_authority).RequireNode();
            var peer = WorldNetworkBinding.For(_client).RequireNode();
            Actor = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
                { State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live };
            node.AddClient(Actor);
            var server = new Client(Guid.Empty);
            var pair = InProcessMessageConnection.CreatePair(node, Actor, peer, server);
            _toClient = pair.ToClient;
            _toAuthority = pair.ToAuthority;
            Pending = (PendingLogicalMessages)typeof(NetNode).GetField(
                "_pendingPackages",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(node)!;
            var characters = new SubsystemCharacters();
            _authority.AddSubsystem(characters);
            Character = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
            typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Character, _authority);
            Character.CharacterId = Actor.CharacterId;
            Character.ComponentCharacter = new ComponentCharacter { ComponentBody = new ComponentBody() };
            ((List<CharacterData>)typeof(SubsystemCharacters).GetField(
                "_characters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(characters)!).Add(Character);
            var terrain = new SubsystemTerrain { Terrain = new Terrain() };
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Demands))!.SetValue(
                terrain,
                new TerrainDemandManager(terrain.Terrain, Game.Terrains.Distribution.TerrainContentRole.Authority)
            );
            _authority.AddSubsystem(Interest);
            Set(Interest, "_subsystemTerrain", terrain);
            Set(Interest, "_subsystemCharacters", characters);
            var blocks = new SubsystemBlockEntities();
            _authority.AddSubsystem(blocks);
            var inventories = new SubsystemInventories();
            _authority.AddSubsystem(inventories);
            _authority.AddSubsystem(new SubsystemGameInfo { WorldSettings = new WorldSettings { GameMode = mode } });
            _authority.InitializeComponent(inventory);
            inventory.Entity.EntityId = 42;
            typeof(ComponentInventoryBase).GetProperty(nameof(ComponentInventoryBase.Id))!.SetValue(inventory, 7);
            inventories.RegisterInventory(inventory);
            var definition =
                new DatabaseObject(_authority.GameDatabase.MemberComponentTemplateType, nameof(ComponentBlockEntity))
                    { NestingParent = inventory.Entity.ValuesDictionary.DatabaseObject };
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(definition);
            var block = new ComponentBlockEntity { Coordinates = _point };
            block.Initialize(inventory.Entity, values);
            var components =
                (List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(inventory.Entity)!;
            components.Add(inventory);
            components.Add(block);
            blocks.BlockEntities.Add(_point, block);
        }

        public ComponentInventoryBase Inventory { get; }

        public CharacterData Character { get; }

        public Client Actor { get; }

        public SubsystemNetworkInterest Interest { get; } = new();

        public PendingLogicalMessages Pending { get; }

        public void Request(bool byPoint, bool roundTrip)
        {
            BlockEditPackage package = byPoint
                ? new OpenBlockInventoryPackage { Point3 = _point }
                : new OpenInventoryPackage { InventoryId = Inventory.Id };
            if (roundTrip)
            {
                package = (BlockEditPackage)ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(package));
            }

            var node = WorldNetworkBinding.For(_authority).RequireNode();
            new BlockEditPackageHandler().Handle(package, new PackageReceiveContext(node, node.Runtime, Actor));
        }

        public void Dispose()
        {
            _toClient.Dispose();
            _toAuthority.Dispose();
            // The roster is a real lookup fact, not a loaded character lifecycle fixture.
            ((List<CharacterData>)typeof(SubsystemCharacters).GetField(
                "_characters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(_authority.FindSubsystem<SubsystemCharacters>(true)!)!).Clear();
            _authority.Dispose();
            _client.Dispose();
        }

        private static void Set(object owner, string name, object value) =>
            owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
    }
}
