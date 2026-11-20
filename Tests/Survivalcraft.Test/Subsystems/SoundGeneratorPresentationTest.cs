using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.ElectricElements;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Replication;
using Game.Network.Transport;
using Game.ParticleSystems;
using Game.Subsystems;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class SoundGeneratorPresentationTest
{
    private static SoundGeneratorNote Note =>
        new(new Point3(1, 65, 1), 4, SoundGeneratorInstrument.Drums, 0, 0, 15, 0f);

    [Theory]
    [InlineData(SoundGeneratorInstrument.Bell, 5, 0, "Audio/SoundGenerator/BellC5")]
    [InlineData(SoundGeneratorInstrument.Organ, 4, 0, "Audio/SoundGenerator/OrganC4")]
    [InlineData(SoundGeneratorInstrument.AlternatePiano, 6, 0, "Audio/SoundGenerator/PianoC6")]
    [InlineData(SoundGeneratorInstrument.Drums, 0, 0, "Audio/SoundGenerator/DrumsSnare")]
    [InlineData(SoundGeneratorInstrument.Drums, 0, 9, "Audio/SoundGenerator/DrumsHandClap")]
    public void TypedSelectorsResolveOnlyTheExistingSoundAssets(
        SoundGeneratorInstrument instrument,
        int octave,
        int noteInput,
        string path
    )
    {
        var note = Note with { Instrument = instrument, SampleOctave = octave, NoteInput = noteInput };
        var package = new SoundGeneratorNotePackage { Note = note };
        var restored =
            Assert.IsType<SoundGeneratorNotePackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(package)));
        Assert.Equal(note, restored.Note);
        Assert.Equal(path, SubsystemSoundGeneratorPresentation.GetSoundName(restored.Note));
        Assert.Equal(PackageTransportPolicy.Effect, PackageTransportPolicy.Get(package));
    }

    [Fact]
    public void InvalidSelectorsAndNumericInputsAreRejectedBeforeAnyAssetLookup()
    {
        var invalid = new[]
        {
            Note with { Instrument = (SoundGeneratorInstrument)11 },
            Note with { Instrument = SoundGeneratorInstrument.Organ, SampleOctave = 6 },
            Note with { NoteInput = 10 }, Note with { Face = 6 }, Note with { VolumeInput = 16 },
            Note with { Pitch = float.NaN }, Note with { Pitch = 1.1f }, Note with { Point = new Point3(1, -1, 1) }
        };
        foreach (var note in invalid)
        {
            Assert.False(note.IsValid);
            Assert.Throws<ArgumentException>(() => SubsystemSoundGeneratorPresentation.GetSoundName(note));
            Assert.Throws<InvalidDataException>(() =>
                ProtocolMessageCodec.Encode(new SoundGeneratorNotePackage { Note = note })
            );
        }
    }

    [Fact]
    public void AuthoritySimulatesAndPublishesOneNoteToTheRealInProcessObserverWithoutMedia()
    {
        using var authorityProject = new RuntimeTestProject(RuntimeSide.Authority);
        using var replicaProject = new RuntimeTestProject(RuntimeSide.Client);
        var authority = WorldNetworkBinding.For(authorityProject).RequireNode();
        var replica = WorldNetworkBinding.For(replicaProject).RequireNode();
        var observer = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
            { State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live };
        authority.AddClient(observer);
        var server = new Client(Guid.Empty);
        var connections = InProcessMessageConnection.CreatePair(authority, observer, replica, server);
        using var toClient = connections.ToClient;
        using var toAuthority = connections.ToAuthority;
        var interest = new SubsystemNetworkInterest();
        authorityProject.AddSubsystem(interest);
        ((SpatialInterestIndex)Field(interest, "_spatial")!).SetLocation(observer, Vector2.Zero, 32f);
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0);
        var electricity = new SubsystemElectricity();
        authorityProject.InitializeSubsystem(electricity);
        typeof(SubsystemElectricity).GetProperty(nameof(SubsystemElectricity.SubsystemTerrain))!
            .SetValue(electricity, new SubsystemTerrain { Terrain = terrain });
        typeof(SubsystemElectricity).GetProperty(nameof(SubsystemElectricity.SubsystemTime))!
            .SetValue(electricity, new SubsystemTime());
        var noise = new SubsystemNoise();
        authorityProject.AddSubsystem(new SubsystemBodies());
        authorityProject.AddSubsystem(noise);
        noise.Load(new ValuesDictionary());
        var face = new CellFace(1, 65, 1, 4);
        var element = new SoundGeneratorElectricElement(electricity, face);
        var connector = Enumerable.Range(0, 6).First(value =>
            SubsystemElectricity.GetConnectorDirection(4, 0, value) is ElectricConnectorDirection.In
                or ElectricConnectorDirection.Bottom
        );
        element.Connections.Add(
            new ElectricConnection
            {
                ConnectorFace = connector,
                ConnectorType = ElectricConnectorType.Input,
                NeighborConnectorType = ElectricConnectorType.Output,
                NeighborElectricElement = new Voltage(electricity, face, 9f / 15f)
            }
        );
        Assert.False(element.Simulate());
        Assert.False(element.Simulate());
        Assert.Equal(1, authority.PendingPackageCount);
        typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
            authority,
            null
        );
        SoundGeneratorNote? received = null;
        Assert.Equal(
            1,
            WorldNetworkBinding.For(replicaProject).Inbox.Drain(
                10,
                message =>
                    received = Assert.IsType<SoundGeneratorNotePackage>(message.Package).Note
            )
        );
        var actual = Assert.IsType<SoundGeneratorNote>(received);
        Assert.Equal(SoundGeneratorInstrument.Drums, actual.Instrument);
        Assert.Equal(0f, actual.Pitch);
        Assert.Equal(1f, actual.Volume);
        Assert.Equal(5.5f, actual.MinDistance);
        Assert.Null(authorityProject.FindSubsystem<SubsystemAudio>());
        Assert.Null(authorityProject.FindSubsystem<SubsystemParticles>());
        // Persistent voltage baselines neither contain notes nor queue historical effects.
        var replicaElectricity = new SubsystemElectricity();
        replicaProject.InitializeSubsystem(replicaElectricity);
        replicaElectricity.ReceiveNetworkSnapshot(new ElectricitySnapshot { IsBaseline = true });
        Assert.Equal(0, replica.PendingPackageCount);
        Assert.Equal(0, authority.PendingPackageCount);
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, false)]
    [InlineData(RuntimeSide.Client, false)]
    public void WrongDirectionAndNonPresentationWorldsIgnoreNotesWithoutMedia(RuntimeSide side, bool hasPresentation)
    {
        using var project = new RuntimeTestProject(side, new WorldRuntimeContext(side, hasPresentation));
        new SoundGeneratorNotePackageHandler().Handle(
            new SoundGeneratorNotePackage { Note = Note },
            new PackageReceiveContext(null, WorldRuntimeContext.For(project), null)
        );
        Assert.Null(project.FindSubsystem<SubsystemSoundGeneratorPresentation>());
    }

    [Fact]
    public void ClientSimulationCannotReplayNotesOrRequirePresentationAssets()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        project.AddSubsystem(new SubsystemNoise());
        var electricity = new SubsystemElectricity();
        project.InitializeSubsystem(electricity);
        var element = new SoundGeneratorElectricElement(electricity, new CellFace(1, 65, 1, 4));
        Assert.False(element.Simulate());
        Assert.Null(project.FindSubsystem<SubsystemAudio>());
        Assert.Null(project.FindSubsystem<SubsystemParticles>());
        Assert.Equal(0, WorldNetworkBinding.For(project).RequireNode().PendingPackageCount);
    }

    [Fact]
    public void OwnerResetAndTerrainCloseReleaseEmitterParticlesWithoutReplayingNotes()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var electricity = new SubsystemElectricity();
        var terrain = new SubsystemTerrain();
        var particles = new SubsystemParticles();
        project.AddSubsystem(electricity);
        project.AddSubsystem(terrain);
        project.AddSubsystem(particles);
        project.AddSubsystem(new SubsystemAudio());
        var owner = new SubsystemSoundGeneratorPresentation();
        project.InitializeSubsystem(owner);
        owner.Load(new ValuesDictionary());
        var emitters = (Dictionary<(Point3 Point, int Face), SoundParticleSystem>)Field(owner, "_emitters")!;
        var first = (SoundParticleSystem)RuntimeHelpers.GetUninitializedObject(typeof(SoundParticleSystem));
        var second = (SoundParticleSystem)RuntimeHelpers.GetUninitializedObject(typeof(SoundParticleSystem));
        var third = (SoundParticleSystem)RuntimeHelpers.GetUninitializedObject(typeof(SoundParticleSystem));
        particles.AddParticleSystem(first);
        particles.AddParticleSystem(second);
        particles.AddParticleSystem(third);
        emitters.Add((new Point3(1, 65, 1), 4), first);
        emitters.Add((new Point3(33, 65, 1), 4), second);
        emitters.Add((new Point3(65, 65, 1), 4), third);
        owner.ClearChunkEffects(new Point2(0, 0));
        owner.ClearChunkEffects(new Point2(0, 0));
        Assert.Null(first.SubsystemParticles);
        Assert.Same(particles, second.SubsystemParticles);
        electricity.OnElectricElementBlockRemoved(33, 65, 1);
        Assert.Null(second.SubsystemParticles);
        electricity.ReceiveNetworkSnapshot(new ElectricitySnapshot { IsBaseline = true });
        Assert.Single(emitters);
        terrain.Dispose();
        Assert.Null(third.SubsystemParticles);
        Assert.Empty(emitters);
        Assert.Null(Field(electricity, "ElectricBlockRemoved"));
        Assert.Null(Field(electricity, "ElectricChunkDiscarded"));
        owner.PlayNote(Note);
        Assert.Empty(emitters);
    }

    private static object? Field(object instance, string name)
    {
        return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);
    }

    private sealed class Voltage(SubsystemElectricity electricity, CellFace face, float voltage)
        : ElectricElement(electricity, face)
    {
        public override float GetOutputVoltage(int face) => voltage;
    }
}
