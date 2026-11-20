using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Characters;
using Game.Components;
using Game.Rendering;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class LocalPlayerComponentSchedulingTest
{
    [Theory]
    [InlineData(RuntimeSide.Client, true, true, true)]
    [InlineData(RuntimeSide.Client, true, false, false)]
    [InlineData(RuntimeSide.Authority, true, true, false)]
    [InlineData(RuntimeSide.Authority, true, false, false)]
    [InlineData(RuntimeSide.Authority, false, true, false)]
    [InlineData(RuntimeSide.Client, false, true, false)]
    public void OnlyLocalCharactersInPresentationWorldsScheduleGuiAndInput(
        RuntimeSide side,
        bool presentationAvailable,
        bool isLocal,
        bool expected
    )
    {
        var hasPresentation = side == RuntimeSide.Client && presentationAvailable;
        using var project = new RuntimeTestProject(side, new WorldRuntimeContext(side, hasPresentation));
        var characterData = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        // Deliberately mark Authority characters local to ensure world ownership wins.
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(characterData, isLocal);
        var character = new ComponentCharacter { CharacterData = characterData };
        project.InitializeComponent(character);
        var gui = (ComponentGui)RuntimeHelpers.GetUninitializedObject(typeof(ComponentGui));
        gui.ComponentCharacter = character;
        var input = new ComponentInput { ComponentCharacter = character };

        Assert.Equal(expected, SubsystemUpdate.ShouldScheduleUpdateable(gui, WorldRuntimeContext.For(project)));
        Assert.Equal(expected, SubsystemUpdate.ShouldScheduleUpdateable(input, WorldRuntimeContext.For(project)));
        Assert.Equal(expected, SubsystemDrawing.ShouldScheduleDrawable(gui));
        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(character, WorldRuntimeContext.For(project)));
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, 0)]
    [InlineData(RuntimeSide.Client, 5)]
    public void PresentationSubsystemsAreRegisteredOnlyInTheirPresentationWorld(RuntimeSide side, int expected)
    {
        using var project = new RuntimeTestProject(side);
        var update = new SubsystemUpdate();
        project.InitializeSubsystem(update);
        project.AddSubsystem(new SubsystemTime());
        project.AddSubsystem(new SubsystemCharacters());
        project.AddSubsystem(new SubsystemAudio());
        project.AddSubsystem(new SubsystemAmbientSounds());
        project.AddSubsystem(new SubsystemParticles());
        project.AddSubsystem(new SubsystemAnimatedTextures());
        project.AddSubsystem(new SubsystemGameWidgets());

        update.Load(new ValuesDictionary());

        var pending = (Dictionary<IUpdateable, bool>)typeof(SubsystemUpdate)
            .GetField("_toAddOrRemove", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(update)!;
        Assert.Equal(expected, pending.Keys.Count(item => item.UpdateSide == UpdateExecutionSide.Presentation));
        var runtime = WorldRuntimeContext.For(project);
        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(new SubsystemSky(), runtime));
        Assert.True(SubsystemUpdate.ShouldScheduleUpdateable(new SubsystemWeather(), runtime));
        runtime.Close();
        Assert.False(SubsystemUpdate.ShouldScheduleUpdateable(new SubsystemSky(), runtime));
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, 0)]
    [InlineData(RuntimeSide.Client, 5)]
    public void VisualCharacterComponentsAreRegisteredOnlyForPresentation(RuntimeSide side, int expected)
    {
        using var project = new RuntimeTestProject(side);
        var update = new SubsystemUpdate();
        project.InitializeSubsystem(update);
        var components = new Component[]
        {
            new ComponentFirstPersonModel(), new ComponentVrHandsModel(),
            new ComponentAimingSights(), new ComponentScreenOverlays(), new ComponentBlockHighlight()
        };
        foreach (var component in components)
        {
            project.InitializeComponent(component);
            ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(component.Entity)!).Add(component);
            update.OnEntityAdded(component.Entity);
        }

        var pending = (Dictionary<IUpdateable, bool>)typeof(SubsystemUpdate)
            .GetField("_toAddOrRemove", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(update)!;
        Assert.Equal(expected, pending.Keys.Count(item => item.UpdateSide == UpdateExecutionSide.Presentation));
        Assert.Equal(
            side == RuntimeSide.Authority,
            SubsystemUpdate.ShouldScheduleUpdateable(new ComponentClothing(), WorldRuntimeContext.For(project))
        );

        Assert.Equal(
            side == RuntimeSide.Client,
            SubsystemUpdate.ShouldScheduleUpdateable(
                new ComponentClothingPresentation(),
                WorldRuntimeContext.For(project)
            )
        );
        if (side == RuntimeSide.Authority)
        {
            ((ComponentVrHandsModel)components[1]).Load(new ValuesDictionary(), null!);
            foreach (var drawable in components.Cast<IDrawable>()) { drawable.Draw(null!, 0); }
        }
    }
}
