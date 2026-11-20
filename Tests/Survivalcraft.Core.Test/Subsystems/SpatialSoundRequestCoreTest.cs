using Engine.Core.Mathematics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.World;

namespace Survivalcraft.Core.Test.Subsystems;

public sealed class SpatialSoundRequestCoreTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority)]
    [InlineData(RuntimeSide.Client)]
    public void SpatialFactsRetainIntentWithoutAudioDeviceOrTransport(RuntimeSide side)
    {
        using var project = new SoundProject(side);
        var facts = new List<SpatialSoundRequest>();
        project.Source.Requested += facts.Add;
        project.Source.RequestSound("Audio/Test", .8f, -.2f, new Vector3(1, 2, 3), 4f, true);
        project.Source.RequestRandomSound("Audio/Directory", .7f, .1f, Vector3.Zero, 3f, 2f);
        Assert.Equal(2, facts.Count);
        Assert.True(facts[0].AutoDelay);
        Assert.False(facts[0].RandomDirectory);
        Assert.Equal(-.2f, facts[0].Pitch);
        Assert.Equal(new Vector3(1, 2, 3), facts[0].Position);
        Assert.True(facts[1].RandomDirectory);
        Assert.False(facts[1].AutoDelay);
        Assert.Equal(2f, facts[1].DelaySeconds);
        project.Runtime.Close();
        project.Source.RequestSound("closed", 1, 0, Vector3.Zero, 1, true);
        Assert.Equal(2, facts.Count);
    }

    private sealed class SoundProject : Project
    {
        public SoundProject(RuntimeSide side)
        {
            var names = new[]
            {
                "Folder", "ProjectTemplate", "MemberSubsystemTemplate", "SubsystemTemplate", "EntityTemplate",
                "MemberComponentTemplate", "ComponentTemplate", "ParameterSet", "Parameter"
            };
            var types = names.Select(name => new DatabaseObjectType(
                    name,
                    name,
                    name,
                    0,
                    name == "Parameter",
                    false,
                    64,
                    false
                )
            ).ToArray();
            foreach (var type in types) { type.InitializeRelations(types, null, types[^1]); }

            var root = new DatabaseObject(types[0], "Root");
            GameDatabase = new GameDatabase(new Database(root, types));
            ProjectTemplate = new DatabaseObject(types[1], "Sound") { NestingParent = root };
            Runtime = new WorldRuntimeContext(side, false);
            Runtime.BindProject(this);
            var definition = new DatabaseObject(
                GameDatabase.MemberSubsystemTemplateType,
                nameof(SubsystemSpatialSoundRequests)
            ) { NestingParent = ProjectTemplate };
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(definition);
            Source.Initialize(this, values);
            subsystems.Add(Source);
        }

        public WorldRuntimeContext Runtime { get; }

        public SubsystemSpatialSoundRequests Source { get; } = new();
    }
}
