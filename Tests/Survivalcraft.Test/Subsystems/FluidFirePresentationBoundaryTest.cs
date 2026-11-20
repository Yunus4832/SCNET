using System.Reflection;

using Engine.Core.Collections;
using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class FluidFirePresentationBoundaryTest
{
    [Fact]
    public void RealAuthorityFluidAndFireLoadAndUpdateWithoutAudioViewsOrParticles()
    {
        using var scope = new WorldCatalogScope(RuntimeSide.Authority);
        var project = scope.Project;
        project.AddSubsystem(new SubsystemTerrain { Terrain = new Terrain() });
        project.AddSubsystem(new SubsystemNetworkInterest());
        project.AddSubsystem(new SubsystemTime());
        var fire = new SubsystemFireBlockBehavior();
        var water = new SubsystemWaterBlockBehavior();
        var magma = new SubsystemMagmaBlockBehavior();
        project.AddSubsystem(fire);
        project.AddSubsystem(water);
        project.AddSubsystem(magma);
        fire.Load(new ValuesDictionary());
        water.Load(new ValuesDictionary());
        magma.Load(new ValuesDictionary());
        fire.Update(.1f);
        water.Update(.1f);
        magma.Update(.1f);
        Assert.Equal(UpdateExecutionSide.Authority, fire.UpdateSide);
        Assert.Equal(UpdateExecutionSide.Authority, water.UpdateSide);
        Assert.Equal(UpdateExecutionSide.Authority, magma.UpdateSide);
        Assert.Null(project.FindSubsystem<SubsystemAudio>());
        Assert.Null(project.FindSubsystem<SubsystemAmbientSounds>());
        Assert.Null(project.FindSubsystem<SubsystemParticles>());
        Assert.Null(project.FindSubsystem<SubsystemGameWidgets>());
    }

    [Fact]
    public void ReplicaWithoutPresentationIgnoresBurnEffectsWithoutCreatingMediaServices()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        using var project = new RuntimeTestProject(RuntimeSide.Client, node.Runtime);
        new ComponentOnFirePackageHandler().Handle(
            new BlockBurnEffectPackage { Position = Vector3.Zero, Kind = BlockBurnEffectKind.Burning },
            new PackageReceiveContext(node, node.Runtime, null)
        );
        Assert.Null(project.FindSubsystem<SubsystemFirePresentation>());
        Assert.Null(project.FindSubsystem<SubsystemAudio>());
        Assert.Null(project.FindSubsystem<SubsystemParticles>());
    }

    [Fact]
    public void AmbientFireUsesLiveReplicaRecordsAndRebuildsAfterChunkReset()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var fire = new SubsystemFireBlockBehavior();
        var ambient = new SubsystemAmbientSounds();
        var audio = new SubsystemAudio();
        project.AddSubsystem(fire);
        project.AddSubsystem(ambient);
        project.AddSubsystem(audio);
        project.AddSubsystem(new SubsystemTerrain());
        project.AddSubsystem(new SubsystemParticles());
        project.AddSubsystem(new SubsystemGameWidgets());
        ((List<Vector3>)Field(audio, "_listenerPositions")!).Add(Vector3.Zero);
        var presentation = new SubsystemFirePresentation();
        project.InitializeSubsystem(presentation);
        presentation.Load(new ValuesDictionary());
        fire.AddFireNet(1, 0, 0, 1f);
        presentation.Update(1f);
        Assert.InRange(ambient.FireSoundVolume, .74257f, .74258f);
        Assert.Single(fire.FirePoints);
        fire.ClearChunkState(new Point2(0, 0));
        ambient.FireSoundVolume = 0f;
        presentation.Update(1f);
        Assert.Equal(0f, ambient.FireSoundVolume);
        fire.AddFireNet(2, 0, 0, 1f);
        presentation.Update(1f);
        Assert.InRange(ambient.FireSoundVolume, .18703f, .18704f);
        Assert.Equal(new Point3(2, 0, 0), Assert.Single(fire.FirePoints));
        presentation.Dispose();
        Assert.Empty((DynamicArray<Point3>)Field(presentation, "_firePointsCopy")!);
        ambient.FireSoundVolume = 0f;
        presentation.Update(10f);
        presentation.PlayBurnEffect(Vector3.Zero, BlockBurnEffectKind.Burning);
        Assert.Equal(0f, ambient.FireSoundVolume);
        Assert.Single(fire.FirePoints);
    }

    private static object? Field(object instance, string name)
    {
        return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);
    }
}
