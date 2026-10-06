using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core;

using EntitySystem.Core;

using Game;
using Game.Components;
using Game.Subsystems;
using Game.Terrains;

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
        terrain.AllocateChunk(0, 0).MainThreadState = mountReady
            ? TerrainChunkState.InvalidLight : TerrainChunkState.NotLoaded;
        terrain.AllocateChunk(2, 0).MainThreadState = riderReady
            ? TerrainChunkState.InvalidLight : TerrainChunkState.NotLoaded;
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

    private static ComponentBody CreateBody(SubsystemTerrain terrain, Vector3 position)
    {
        var body = new ComponentBody { Position = position, Rotation = Quaternion.Identity };
        typeof(ComponentBody).GetField("_subsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(body, terrain);
        return body;
    }
}
