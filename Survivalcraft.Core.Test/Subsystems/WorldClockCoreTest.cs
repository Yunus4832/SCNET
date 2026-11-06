using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.World;
using Game.World.Timing;

namespace Survivalcraft.Core.Test.Subsystems;

public sealed class WorldClockCoreTest
{
    [Theory]
    [InlineData(1, 1, 0, 20)]
    [InlineData(2, 1, 1, 20)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(2, 1, 0, 1)]
    public void ActualClockDecidesPacingFromPopulationFacts(int total, int sleeping, int dead, int expected)
    {
        using var project = new ClockProject(RuntimeSide.Authority);
        var time = new SubsystemTime();
        project.Add(time);
        Assert.Equal(expected, time.NextFrame(.016f, () => new CharacterSleepPopulation(total, sleeping, dead)));
        Assert.Equal(expected == 20 ? .05f : null, time.FixedTimeStep);
    }

    [Fact]
    public void PopulationIsReadAfterDelayedCallbacksAndClientNeverReadsAoiPopulation()
    {
        using var authority = new ClockProject(RuntimeSide.Authority);
        var time = new SubsystemTime { FixedTimeStep = .05f };
        authority.Add(time);
        var population = new CharacterSleepPopulation(1, 1, 0);
        time.QueueGameTimeDelayedExecution(0, () => population = new CharacterSleepPopulation(1, 0, 0));
        Assert.Equal(1, time.NextFrame(.016f, () => population));
        using var client = new ClockProject(RuntimeSide.Client);
        var replica = new SubsystemTime();
        client.Add(replica);
        Assert.Equal(1, replica.NextFrame(.016f, () => throw new InvalidOperationException("AOI population read")));
    }

    [Fact]
    public void CoreGameInfoPublishesClockFactsWithoutTransportOrHosting()
    {
        using var project = new ClockProject(RuntimeSide.Authority);
        var time = new SubsystemTime();
        var info = new SubsystemGameInfo();
        var solar = new SubsystemTimeOfDay();
        project.Add(time);
        project.Add(info);
        project.Add(solar);
        var data = new ValuesDictionary
        {
            { "WorldName", "Core" }, { "WorldDirectoryName", "memory" },
            { "WorldSeed", 1 }, { "TotalElapsedGameTime", 100d }
        };
        info.Load(data);
        solar.Load(new ValuesDictionary { { "TimeOfDayOffset", .4d } });
        var facts = new List<WorldTimeSnapshot>();
        info.TimeSynchronizationRequested += facts.Add;
        for (var i = 0; i < 10; i++)
        {
            time.NextFrame(.1f, () => default);
            info.Update(time.GameTimeDelta);
        }

        var snapshot = Assert.Single(facts);
        Assert.InRange(snapshot.TotalElapsedGameTime, 100.99, 101.01);
        Assert.Equal(.4d, snapshot.TimeOfDayOffset);
        var saved = new ValuesDictionary();
        info.Save(saved);
        Assert.False(saved.ContainsKey("RunServer"));
        Assert.False(saved.ContainsKey("P2P"));
        Assert.Null(typeof(SubsystemGameInfo).GetProperty("Hosting"));
    }

    private sealed class ClockProject : Project
    {
        public ClockProject(RuntimeSide side)
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
            ProjectTemplate = new DatabaseObject(types[1], "Clock") { NestingParent = root };
            new WorldRuntimeContext(side, false).BindProject(this);
        }

        public void Add(Subsystem subsystem)
        {
            var definition = new DatabaseObject(GameDatabase.MemberSubsystemTemplateType, subsystem.GetType().Name)
                { NestingParent = ProjectTemplate };
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(definition);
            subsystem.Initialize(this, values);
            subsystems.Add(subsystem);
        }
    }
}
