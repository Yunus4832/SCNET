using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Characters;
using Game.Components;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class LevelOwnershipTest
{
    [Fact]
    public void AuthorityExperienceCrossesLevelWithoutAudioOrGuiDependencies()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var level = CreateLevel(project, out var character);
        character.CharacterData.Level = 1.99f;
        level.AddExperience(1, true);
        Assert.Equal(2.002f, character.CharacterData.Level, 5);
        Assert.Equal(character.CharacterData.Level, character.CharacterStats.HighestLevel);
        var presentation = new ComponentLevelPresentation();
        project.InitializeComponent(presentation);
        presentation.Load(new ValuesDictionary(), null!);
        presentation.OnEntityAdded();
        presentation.Update(1f);
    }

    [Fact]
    public void ClientAppliesExactAuthorityStateWithoutRecalculatingExperience()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var level = CreateLevel(project, out var character);
        var notifications = new List<(int, bool)>();
        level.ExperienceReceived += (ups, sound) => notifications.Add((ups, sound));
        level.AddExperience(100, true);
        Assert.Equal(1f, character.CharacterData.Level);
        level.ApplyExperienceState(3.12345f, 2, true);
        Assert.Equal(3.12345f, character.CharacterData.Level);
        Assert.Equal(3.12345f, character.CharacterStats.HighestLevel);
        Assert.Equal((2, true), Assert.Single(notifications));
    }

    [Fact]
    public void AuthorityCannotConsumeClientLevelStateAndClosedWorldCannotChangeLevel()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var level = CreateLevel(project, out var character);
        level.ApplyExperienceState(99, 1, true);
        Assert.Equal(1f, character.CharacterData.Level);
        WorldRuntimeContext.For(project).Close();
        level.AddExperience(100, true);
        Assert.Equal(1f, character.CharacterData.Level);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OldDelayedFeedbackCannotRunAfterRemovalOrDisposalAndReaddition(bool dispose)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var level = CreateLevel(project, out var character);
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(
            character.CharacterData,
            true
        );
        var presentation = new ComponentLevelPresentation();
        project.InitializeComponent(presentation);
        SetField(presentation, "_level", level);
        SetField(presentation, "_character", character);
        presentation.OnEntityAdded();
        level.ApplyExperienceState(2, 1, false);
        var runtime = WorldRuntimeContext.For(project);
        Assert.NotEmpty(
            (System.Collections.IEnumerable)typeof(WorldRuntimeContext)
                .GetField("_delayedActions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!
        );
        if (dispose) { presentation.Dispose(); }
        else { presentation.OnEntityRemoved(); }

        presentation.OnEntityAdded();
        // Playback dependencies are intentionally absent. Stale callbacks must never reach them.
        runtime.RunTimeDelayedExecutions(double.MaxValue);
    }

    private static ComponentLevel CreateLevel(RuntimeTestProject project, out TestCharacter character)
    {
        project.AddSubsystem(new SubsystemTime());
        project.AddSubsystem(new SubsystemGameInfo());
        project.AddSubsystem(new SubsystemNetworkInterest());
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(data, project);
        data.CharacterId = Guid.NewGuid();
        data.Level = 1;
        character = new TestCharacter { CharacterData = data };
        project.InitializeComponent(character);
        var level = new ComponentLevel();
        project.InitializeComponent(level);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(level.Entity)!).AddRange([character, new ComponentBody()]);
        level.Load(new ValuesDictionary(), null!);
        return level;
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }

    private sealed class TestCharacter : ComponentCharacter
    {
        public override CharacterStats CharacterStats { get; } = new();
    }
}
