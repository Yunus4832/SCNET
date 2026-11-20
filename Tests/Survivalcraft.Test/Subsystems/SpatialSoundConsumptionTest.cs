using System.Collections;
using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class SpatialSoundConsumptionTest
{
    [Fact]
    public void SourceRequestUsesActualClientAttenuationQueueAndDisposeUnsubscribes()
    {
        using var fixture = new Fixture();
        fixture.Requests.RequestSound("Audio/Test", .8f, -.15f, new Vector3(2, 0, 0), 2f, 3f);
        var queued = Assert.Single(fixture.Queue.Cast<SubsystemAudio.SoundInfo>());
        Assert.Equal("Audio/Test", queued.Name);
        Assert.Equal(.8f, queued.Volume);
        Assert.Equal(-.15f, queued.Pitch);
        Assert.Equal(3d, queued.Time);
        fixture.Audio.Dispose();
        fixture.Requests.RequestSound("after", 1, 0, Vector3.Zero, 1, true);
        Assert.Empty(fixture.Queue);
    }

    [Fact]
    public void NearestSpikesAreChosenAtClientFlushAndSamplePitchOnlyForSelectedRequest()
    {
        using var fixture = new Fixture();
        fixture.Requests.RequestNearestSound("game:spikes", "Audio/Spikes", .7f, .1f, new Vector3(10, 0, 0), 4f, true);
        fixture.Requests.RequestNearestSound("game:spikes", "Audio/Spikes", .7f, .1f, new Vector3(2, 0, 0), 4f, true);
        Assert.Empty(fixture.Queue);
        typeof(SubsystemAudio).GetMethod("FlushNearestRequests", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(fixture.Audio, null);
        var queued = Assert.Single(fixture.Queue.Cast<SubsystemAudio.SoundInfo>());
        Assert.Equal(.7f, queued.Volume);
        Assert.InRange(queued.Pitch, -.1f, .1f);
        Assert.Equal(.02d, queued.Time, 5);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly RuntimeTestProject _project = new(RuntimeSide.Client);

        public Fixture()
        {
            _project.AddSubsystem(Requests);
            _project.AddSubsystem(new SubsystemTime());
            _project.AddSubsystem(new SubsystemGameWidgets());
            _project.AddSubsystem(Audio);
            Audio.Load(new ValuesDictionary());
            ((List<Vector3>)typeof(SubsystemAudio).GetField(
                "_listenerPositions",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(Audio)!).Add(Vector3.Zero);
        }

        public SubsystemSpatialSoundRequests Requests { get; } = new();

        public SubsystemAudio Audio { get; } = new();

        public IList Queue =>
            (IList)typeof(SubsystemAudio).GetField("_queuedSounds", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Audio)!;

        public void Dispose() => _project.Dispose();
    }
}
