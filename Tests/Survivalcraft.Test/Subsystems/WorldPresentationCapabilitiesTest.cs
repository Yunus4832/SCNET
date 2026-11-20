using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Cameras;
using Game.ParticleSystems;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldPresentationCapabilitiesTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority, true)]
    [InlineData(RuntimeSide.Authority, false)]
    [InlineData(RuntimeSide.Client, false)]
    public void WorldsWithoutPresentationDoNotRegisterParticles(RuntimeSide side, bool hasPresentation)
    {
        using var project = new Project();
        new WorldRuntimeContext(
            side,
            (side == RuntimeSide.Client && hasPresentation)
        ).BindProject(project);
        var subsystem = new SubsystemParticles();
        WorldSubsystemTestSupport.Attach(project, subsystem);
        var particle = new TestParticle();

        subsystem.AddParticleSystem(particle);
        subsystem.RemoveParticleSystem(particle);

        Assert.False(subsystem.ContainsParticleSystem(particle));
        Assert.Null(particle.SubsystemParticles);
    }

    [Fact]
    public void AuthorityAmbientSoundsLoadAndUpdateWithoutCreatingAudio()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var audio = new SubsystemAudio();
        project.AddSubsystem(audio);
        var ambient = new SubsystemAmbientSounds
        {
            FireSoundVolume = 1f,
            WaterSoundVolume = 1f,
            MagmaSoundVolume = 1f
        };
        project.AddSubsystem(ambient);

        ambient.Load(new ValuesDictionary());
        ambient.Update(0.1f);

        Assert.Equal(0f, ambient.FireSoundVolume);
        Assert.Equal(0f, ambient.WaterSoundVolume);
        Assert.Equal(0f, ambient.MagmaSoundVolume);
        Assert.Throws<InvalidOperationException>(() => audio.CreateSound("Audio/Fire"));
    }

    private sealed class TestParticle : ParticleSystemBase
    {
        public override bool Simulate(float dt) => false;

        public override void Draw(Camera camera)
        {
        }
    }
}
