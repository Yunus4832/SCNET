using System.Reflection;

using Engine.Core.Mathematics;
using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.Inventories;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class ClothingOwnershipTest
{
    [Fact]
    public void AuthorityClothingLoadsWithoutGraphicsModelsAudioParticlesOrGui()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.AddSubsystem(new SubsystemGameInfo());
        project.AddSubsystem(new SubsystemTime());
        project.AddSubsystem(new SubsystemPickables());
        project.AddSubsystem(new SubsystemInventories());
        var clothing = new ComponentClothing();
        project.InitializeComponent(clothing);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(clothing.Entity)!).AddRange(
            [
                new ComponentBody(), new ComponentVitalStats(), new ComponentLocomotion(), new ComponentCharacter()
            ]
        );
        var clothes = new ValuesDictionary();
        foreach (var slot in Enum.GetValues<ClothingSlot>()) { clothes.SetValue(slot.ToString(), string.Empty); }

        var values = new ValuesDictionary();
        values.SetValue("Clothes", clothes);
        clothing.Load(values, null!);
        Assert.All(Enum.GetValues<ClothingSlot>(), slot => Assert.Empty(clothing.GetClothes(slot)));
        Assert.Equal(UpdateExecutionSide.Authority, clothing.UpdateSide);
        var presentation = new ComponentClothingPresentation();
        project.InitializeComponent(presentation);
        presentation.Load(new ValuesDictionary(), null!);
        presentation.OnEntityAdded();
        presentation.UpdateRenderTargets();
        presentation.ApplyEffect(new ClothingEffectPackage { Kind = ClothingEffectKind.Broken });
        presentation.Dispose();
    }

    [Theory]
    [InlineData(RuntimeSide.Client, false)]
    [InlineData(RuntimeSide.Authority, true)]
    public void ClientOrClosedWorldCannotRunArmorWearEatingOrDrops(RuntimeSide side, bool close)
    {
        using var project = new RuntimeTestProject(side);
        var clothing = new ComponentClothing();
        project.InitializeComponent(clothing);
        if (close) { WorldRuntimeContext.For(project).Close(); }

        clothing.Update(2f);
        Assert.Equal(12f, clothing.ApplyArmorProtection(12f));
        clothing.ProcessSlotItems(null!, 0, 0, 203, 1, 1, out var value, out var count);
        Assert.Equal(0, value);
        Assert.Equal(0, count);
        clothing.DropAllItems(Vector3.Zero);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PresentationUnsubscribesFromClothingAndDisplayOnRemovalOrDisposal(bool dispose)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var clothing = new ComponentClothing();
        var presentation = new ComponentClothingPresentation();
        project.InitializeComponent(presentation);
        typeof(ComponentClothingPresentation).GetField("_clothing", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(presentation, clothing);
        var changed = typeof(ComponentClothing).GetField(
            "ClothesChanged",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        var reset = typeof(Display).GetField("DeviceReset", BindingFlags.Static | BindingFlags.NonPublic)!;
        try
        {
            presentation.OnEntityAdded();
            presentation.OnEntityAdded();
            Assert.Single(((Delegate)changed.GetValue(clothing)!).GetInvocationList());
            Assert.Single(
                ((Delegate)reset.GetValue(null)!).GetInvocationList(),
                d => ReferenceEquals(d.Target, presentation)
            );
            if (dispose) { presentation.Dispose(); }
            else { presentation.OnEntityRemoved(); }

            Assert.Null(changed.GetValue(clothing));
            Assert.DoesNotContain(
                ((Delegate?)reset.GetValue(null))?.GetInvocationList() ?? [],
                d => ReferenceEquals(d.Target, presentation)
            );
            presentation.ApplyEffect(new ClothingEffectPackage { Kind = ClothingEffectKind.Broken });
            presentation.UpdateRenderTargets();
        }
        finally { presentation.Dispose(); }
    }
}
