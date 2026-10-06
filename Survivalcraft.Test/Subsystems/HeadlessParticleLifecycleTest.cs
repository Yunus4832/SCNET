using Engine.Core;

using Game.Cameras;
using Game.ParticleSystems;
using Game.Subsystems;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class HeadlessParticleLifecycleTest
{
    [Fact]
    public void HeadlessParticleAddAndRemoveAreSymmetricNoOps()
    {
        var previous = RunMode.Value;
        try
        {
            RunMode.Value = RunModeType.HeadlessServer;
            var subsystem = new SubsystemParticles();
            var particle = new TestParticle();

            subsystem.AddParticleSystem(particle);
            subsystem.RemoveParticleSystem(particle);

            Assert.False(subsystem.ContainsParticleSystem(particle));
            Assert.Null(particle.SubsystemParticles);
        }
        finally
        {
            RunMode.Value = previous;
        }
    }

    private sealed class TestParticle : ParticleSystemBase
    {
        public override bool Simulate(float dt) => false;

        public override void Draw(Camera camera) { }
    }
}
