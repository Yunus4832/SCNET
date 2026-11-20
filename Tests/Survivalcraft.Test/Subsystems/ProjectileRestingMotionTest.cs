using System.Reflection;

using Engine.Core.Mathematics;

using Game.Subsystems;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class ProjectileRestingMotionTest
{
    [Fact]
    public void RestingSnapshotsStayFixedBetweenUpdatesAndWakeWhenAuthorityResumesMotion()
    {
        using var world = new WorldCatalogScope(RuntimeSide.Client);
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0).HasContents = true;
        var terrainSubsystem = new SubsystemTerrain { Terrain = terrain };
        world.Project.AddSubsystem(terrainSubsystem);
        var projectiles = new SubsystemProjectiles();
        world.Project.AddSubsystem(projectiles);
        typeof(SubsystemProjectiles).GetField("_subsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(projectiles, terrainSubsystem);
        typeof(SubsystemProjectiles).GetField("_subsystemGameInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(projectiles, new SubsystemGameInfo());
        world.Catalog.Blocks[0].ProjectileDamping = 1f;
        var position = new Vector3(8, 64, 8);
        var projectile = projectiles.AddReplicatedProjectile(
            new ProjectileAddedPackage
            {
                NetworkId = 1, StateTick = 1, Position = position,
                Velocity = new Vector3(0, -.13f, 0), AngularVelocity = Vector3.One, IsResting = true
            },
            null
        );
        Assert.True(projectile.IsResting);
        for (uint tick = 2; tick < 12; tick++)
        {
            Assert.True(
                SubsystemProjectiles.ApplyNetworkMotion(
                    projectile,
                    new ProjectileStatePackage
                    {
                        StateTick = tick, Position = position, Velocity = new Vector3(0, -.13f, 0),
                        AngularVelocity = Vector3.One, IsResting = true
                    }
                )
            );
            for (var frame = 0; frame < 6; frame++) { projectiles.Update(1f / 60); }

            Assert.Equal(position, projectile.Position);
            Assert.Equal(Vector3.Zero, projectile.Rotation);
        }

        Assert.False(
            SubsystemProjectiles.ApplyNetworkMotion(
                projectile,
                new ProjectileStatePackage
                {
                    StateTick = 5, Position = position, Velocity = -Vector3.UnitY, IsResting = false
                }
            )
        );
        Assert.True(projectile.IsResting);
        Assert.True(
            SubsystemProjectiles.ApplyNetworkMotion(
                projectile,
                new ProjectileStatePackage
                {
                    StateTick = 12, Position = position, Velocity = -Vector3.UnitY, IsResting = false
                }
            )
        );
        projectiles.Update(1f / 60);
        Assert.True(projectile.Position.Y < position.Y);

        projectile.Position = position;
        projectile.Velocity = Vector3.Zero;
        projectiles.Update(1f / 60);
        projectiles.Update(1f / 60);
        Assert.True(projectile.Position.Y < position.Y);
    }
}
