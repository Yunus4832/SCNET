using Game.Cameras;
using Game.ParticleSystems;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

/// <summary>验证一次性粒子共用槽位，并在移除时释放。</summary>
public sealed class BurstParticleBudgetTest
{
    [Fact]
    public void DifferentBurstsShareCapacityAndRejectedBurstsSkipConstruction()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var particles = new SubsystemParticles();
        project.InitializeSubsystem(particles);
        var first = new ProbeParticleSystem();
        Assert.True(particles.TryAddBurst(WaterSplashParticleSystem.ParticleCount, () => first));
        for (var i = 1; i < SubsystemParticles.burstParticleLimit / WaterSplashParticleSystem.ParticleCount; i++)
        {
            Assert.True(
                particles.TryAddBurst(WaterSplashParticleSystem.ParticleCount, () => new ProbeParticleSystem())
            );
        }

        Assert.False(particles.TryAddBurst(WaterSplashParticleSystem.ParticleCount, () => throw new Exception()));
        particles.RemoveParticleSystem(first);
        for (var i = 0; i < WaterSplashParticleSystem.ParticleCount / BurntDebrisParticleSystem.ParticleCount; i++)
        {
            Assert.True(
                particles.TryAddBurst(BurntDebrisParticleSystem.ParticleCount, () => new ProbeParticleSystem())
            );
        }

        Assert.False(particles.TryAddBurst(BurntDebrisParticleSystem.ParticleCount, () => throw new Exception()));
    }

    private sealed class ProbeParticleSystem : ParticleSystemBase
    {
        public override void Draw(Camera camera)
        {
        }

        public override bool Simulate(float dt) => false;
    }
}
