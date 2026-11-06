using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Characters;
using Game.Components;
using Game.Furniture;
using Game.Modding.Blocks;
using Game.Network;
using Game.ParticleSystems;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class FurniturePresentationBoundaryTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority, 0)]
    [InlineData(RuntimeSide.Client, 1)]
    public void ScanIntentIsPublishedOnlyByTheActualLocalClient(RuntimeSide side, int expected)
    {
        using var project = new RuntimeTestProject(side);
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(data, true);
        var character = new ComponentCharacter { CharacterData = data };
        project.InitializeComponent(character);
        var miner = new ComponentMiner();
        project.InitializeComponent(miner);
        typeof(ComponentMiner).GetProperty(nameof(ComponentMiner.ComponentCharacter))!.SetValue(miner, character);
        var furniture = new SubsystemFurnitureBlockBehavior();
        project.InitializeSubsystem(furniture);
        var calls = 0;
        furniture.ScanRequested += request =>
        {
            Assert.Same(miner, request.Miner);
            calls++;
        };
        furniture.RequestDesignScan(new CellFace(1, 65, 1, 4), Vector3.UnitZ, miner);
        Assert.Equal(expected, calls);
        furniture.Dispose();
    }

    [Fact]
    public void AuthorityFurnitureLoadsWithOnlyActualRuleServices()
    {
        using var node = new NodeLifetime(false);
        using var scope = new WorldCatalogScope(node.Node);
        var project = scope.Project;
        project.AddSubsystem(new SubsystemTerrain { Terrain = new Terrain() });
        project.AddSubsystem(new SubsystemSpatialSoundRequests());
        project.AddSubsystem(new SubsystemSoundMaterials());
        project.AddSubsystem(new SubsystemItemsScanner());
        project.AddSubsystem(new SubsystemGameInfo());
        project.AddSubsystem(new SubsystemPickables());
        var furniture = new SubsystemFurnitureBlockBehavior();
        project.AddSubsystem(furniture);
        var values = new ValuesDictionary();
        values.SetValue("FurnitureDesigns", new ValuesDictionary());
        values.SetValue("FurnitureSets", new ValuesDictionary());
        furniture.Load(values);
        Assert.Null(project.FindSubsystem<SubsystemParticles>());
        Assert.Null(project.FindSubsystem<SubsystemFurniturePresentation>());
        Assert.Empty(furniture.FurnitureSets);
    }

    [Fact]
    public void ScanWorkingCopiesCannotRenameOrReshapeTheLiveDesignBeforeConfirmation()
    {
        var source = new FurnitureDesign(null, null) { Name = "Original" };
        source.SetValues(2, new int[8]);
        var copy = SubsystemFurniturePresentation.CopySourceDesign(source)!;
        copy.Name = "Unsaved";
        copy.SetValues(3, new int[27]);
        Assert.Equal("Original", source.Name);
        Assert.Equal(2, source.Resolution);
    }

    [Fact]
    public void LateDesignInstallationRefreshesTheActualClientCellWithoutRequiringMediaInRules()
    {
        using var node = new NodeLifetime();
        using var scope = new WorldCatalogScope(node.Node);
        var project = scope.Project;
        var terrain = new SubsystemTerrain { Terrain = new Terrain() };
        terrain.Terrain.AllocateChunk(0, 0);
        var furniture = new SubsystemFurnitureBlockBehavior();
        var particles = new SubsystemParticles();
        project.AddSubsystem(terrain);
        project.AddSubsystem(furniture);
        project.AddSubsystem(particles);
        var owner = new SubsystemFurniturePresentation();
        project.InitializeSubsystem(owner);
        owner.Load(new ValuesDictionary());
        var point = new Point3(1, 65, 1);
        var value = Terrain.MakeBlockValue(
            BuiltInBlockRegistry.GetIndex<FurnitureBlock>(),
            0,
            FurnitureBlock.SetDesignIndex(0, 7, 0, false)
        );
        terrain.Terrain.SetCellValueFast(point.X, point.Y, point.Z, value);
        furniture.OnBlockGenerated(value, point.X, point.Y, point.Z, true);
        var applied =
            (Dictionary<Point3, (FurnitureDesign? Design, long Revision, int Value)>)Field(owner, "_applied")!;
        Assert.Null(applied[point].Design);
        var design = new FurnitureDesign(scope.Catalog, terrain) { Index = 7, Name = "Later" };
        design.SetValues(2, new int[8]);
        furniture.InstallNetworkDesign(design);
        Assert.Same(design, applied[point].Design);
        Assert.Equal(design.Revision, applied[point].Revision);
        // No torch content means this real reconciliation requires no graphics resource allocation.
        Assert.Empty((System.Collections.IDictionary)Field(owner, "_particleSystemsByCell")!);
        var fire = (FireParticleSystem)RuntimeHelpers.GetUninitializedObject(typeof(FireParticleSystem));
        particles.AddParticleSystem(fire);
        ((Dictionary<Point3, List<FireParticleSystem>>)Field(owner, "_particleSystemsByCell")!)[point] = [fire];
        design.TerrainUseCount = 1;
        terrain.Terrain.SetCellValueFast(point.X, point.Y, point.Z, 0);
        furniture.OnBlockRemoved(value, 0, point.X, point.Y, point.Z);
        Assert.True(fire.IsStopped);
        Assert.Empty(applied);
        owner.Dispose();
        owner.Dispose();
        Assert.Null(fire.SubsystemParticles);
        Assert.Empty((System.Collections.IDictionary)Field(owner, "_cells")!);
        Assert.Null(Field(furniture, "FurnitureCellChanged"));
    }

    private static object? Field(object instance, string name)
    {
        return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);
    }

    private sealed class NodeLifetime(bool isClient = true) : IDisposable
    {
        internal NetNode Node { get; } = new NetNode(
            NetNodeOptions.Default,
            isClient,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            isClient ? NodeRole.Client : NodeRole.Authority
        );

        public void Dispose() => Node.Runtime.Close();
    }
}
