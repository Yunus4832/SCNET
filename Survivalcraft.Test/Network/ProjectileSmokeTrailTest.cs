using System.Reflection;

using Engine.Core.Mathematics;

using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class ProjectileSmokeTrailTest
{
    [Fact]
    public void AuthorityPublishesSmokeLifetimeWithoutCreatingParticles()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var projectiles = new SubsystemProjectiles();
        project.InitializeSubsystem(projectiles);
        typeof(SubsystemProjectiles).GetField("_subsystemGameInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(projectiles, new SubsystemGameInfo { TotalElapsedGameTime = 10 });
        var projectile = new Projectile { NetworkId = 42 };

        projectiles.SetSmokeTrail(projectile, 1.25f, 7f, Color.Red);
        var captured = ProjectilePackageBuilder.Capture(projectile);
        var restored = Assert.IsType<ProjectileAddedPackage>(
            ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(captured))
        );

        Assert.NotNull(projectile.Trail);
        Assert.True(projectile.Trail.PublishSmokeSnapshot);
        Assert.Equal(1.25f, restored.SmokeTrail!.Size);
        Assert.Equal(17d, restored.SmokeTrail.EndTime);
        Assert.Equal(Color.Red, restored.SmokeTrail.Color);
    }
}
