using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Components;
using Game.Subsystems;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class MountTerrainReadinessTest
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SeparationTimerOnlyRunsWhenBothBodiesCanSimulate(bool riderReady, bool mountReady)
    {
        var terrain = new Terrain();
        terrain.AllocateChunk(0, 0).HasContents = mountReady;
        terrain.AllocateChunk(2, 0).HasContents = riderReady;
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        var mountBody = CreateBody(subsystem, Vector3.Zero);
        var riderBody = CreateBody(subsystem, new Vector3(32f, 0f, 0f));
        var mount = new ComponentMount { ComponentBody = mountBody };
        var mountEntity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
        typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(mountEntity, new List<Component> { mount, new ComponentHealth { Health = 1f } });
        typeof(Component).GetProperty(nameof(Component.Entity))!.SetValue(mountBody, mountEntity);
        typeof(Component).GetProperty(nameof(Component.Entity))!.SetValue(mount, mountEntity);
        riderBody.ParentBody = mountBody;
        var rider = new ComponentRider
        {
            ComponentCreature = new ComponentCreature
            {
                ComponentBody = riderBody,
                ComponentHealth = new ComponentHealth { Health = 1f }
            }
        };

        rider.Update(0.05f);

        var timer = (float)typeof(ComponentRider)
            .GetField("_outOfMountTime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rider)!;
        Assert.Equal(riderReady && mountReady ? 0.05f : 0f, timer);
        Assert.Same(mountBody, riderBody.ParentBody);
    }

    [Fact]
    public void AClientDoesNotAutomaticallyDismountANonPlayerRider()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0).HasContents = true;
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        var mountBody = CreateBody(subsystem, Vector3.Zero);
        var riderBody = CreateBody(subsystem, Vector3.Zero);
        var mount = new ComponentMount { ComponentBody = mountBody };
        var mountEntity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
        typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(mountEntity, new List<Component> { mount, new ComponentHealth { Health = 0f } });
        typeof(Component).GetProperty(nameof(Component.Entity))!.SetValue(mountBody, mountEntity);
        typeof(Component).GetProperty(nameof(Component.Entity))!.SetValue(mount, mountEntity);
        riderBody.ParentBody = mountBody;
        var rider = new ComponentRider
        {
            ComponentCreature = new ComponentCreature
            {
                ComponentBody = riderBody,
                ComponentHealth = new ComponentHealth { Health = 1f }
            }
        };
        project.InitializeComponent(rider);

        rider.Update(0.05f);

        Assert.False(
            (bool)typeof(ComponentRider).GetField("_isDismounting", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(rider)!
        );
        Assert.Equal(0, Game.Network.WorldNetworkBinding.For(project).RequireNode().PendingPackageCount);
        Assert.Same(mountBody, riderBody.ParentBody);
    }

    private static ComponentBody CreateBody(SubsystemTerrain terrain, Vector3 position)
    {
        var body = new ComponentBody { Position = position, Rotation = Quaternion.Identity };
        typeof(ComponentBody).GetField("_subsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(body, terrain);
        return body;
    }
}
