using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class ExplosionPresentationBoundaryTest
{
    [Fact]
    public void SoundReplyCarriesAuthorityPitchAndRequestsClientDistanceDelayWithoutNoiseSimulation()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var requests = new SubsystemSpatialSoundRequests();
        project.AddSubsystem(requests);
        project.AddSubsystem(new SubsystemParticles());
        var presentation = new SubsystemExplosionPresentation();
        project.AddSubsystem(presentation);
        presentation.Load(new ValuesDictionary());
        var facts = new List<SpatialSoundRequest>();
        requests.Requested += facts.Add;
        var packet = new ExplosionSoundPackage { Position = new Vector3(800, 60, -900), Level = 2000, Pitch = .17f };
        var decoded =
            Assert.IsType<ExplosionSoundPackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(packet)));
        var world = WorldRuntimeContext.For(project);
        new ExplosionsPackageHandler().Handle(decoded, new PackageReceiveContext(null, world, null));
        var fact = Assert.Single(facts);
        Assert.Equal(packet.Position, fact.Position);
        Assert.Equal(packet.Pitch, fact.Pitch);
        Assert.True(fact.AutoDelay);
        Assert.Equal(0f, fact.DelaySeconds);
        presentation.Dispose();
        presentation.PlaySound(packet.Position, packet.Level, packet.Pitch);
        Assert.Single(facts);
    }

    [Theory]
    [InlineData(RuntimeSide.Authority)]
    [InlineData(RuntimeSide.Client)]
    public void NonPresentationWorldsIgnoreExplosionGraphicsAndSound(RuntimeSide side)
    {
        using var project = new RuntimeTestProject(side, new WorldRuntimeContext(side, false));
        new ExplosionsPackageHandler().Handle(
            new ExplosionSoundPackage { Level = 1000, Position = Vector3.One },
            new PackageReceiveContext(null, WorldRuntimeContext.For(project), null)
        );
        Assert.Null(project.FindSubsystem<SubsystemExplosionPresentation>());
    }
}
