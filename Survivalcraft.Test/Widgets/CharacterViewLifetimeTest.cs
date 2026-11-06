using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using Game.Characters;
using Game.Subsystems;
using Game.Widgets;

namespace Survivalcraft.Test.Widgets;

public sealed class CharacterViewLifetimeTest
{
    [Fact]
    public void ClosedOldWidgetDoesNotUpdateMeasureDrawOrItsSubtree()
    {
        var character = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        using var view = new CharacterPresentation(new SubsystemCharacterPresentation(), character);
        var widget = CreateWidget(view);
        var updates = 0;
        widget.Children.Add(new CallbackWidget(() => updates++));
        view.Attach(widget);
        view.Dispose();

        widget.Update();
        widget.Draw(null!);
        widget.MeasureForTest(new Vector2(200, 100));
        var cursor = false;
        Widget.UpdateWidgetsHierarchy(widget, ref cursor);
        Assert.Equal(0, updates);
        Assert.False(widget.IsViewActive);
        Assert.Equal(Vector2.Zero, widget.DesiredSize);
        Assert.Throws<ObjectDisposedException>(() => view.GameWidget);
    }

    [Fact]
    public void ClosingFromChildCancelsRemainingChildrenAndOldWidgetInTheSameTraversal()
    {
        var character = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        using var view = new CharacterPresentation(new SubsystemCharacterPresentation(), character);
        var widget = CreateWidget(view);
        var updates = 0;
        widget.Children.Add(new CallbackWidget(() => updates++));
        // Traversal visits children in reverse order, so this closes before the sibling.
        widget.Children.Add(new CallbackWidget(view.Dispose));
        view.Attach(widget);
        var cursor = false;
        Widget.UpdateWidgetsHierarchy(widget, ref cursor);
        Assert.True(view.IsDisposed);
        Assert.Equal(0, updates);
    }

    [Fact]
    public void DisposedOwnerCannotRecreateAnUnattachedView()
    {
        var owner = new SubsystemCharacterPresentation();
        owner.Dispose();
        var character = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        Assert.Throws<ObjectDisposedException>(() => owner.Get(character));
        owner.Update(0f);
    }

    [Fact]
    public void RebindingClosesTheOldWidget()
    {
        var character = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        using var view = new CharacterPresentation(new SubsystemCharacterPresentation(), character);
        var old = CreateWidget(view);
        view.Attach(old);
        var replacement = CreateWidget(view);
        view.Attach(replacement);
        old.Update();
        old.Draw(null!);
        Assert.False(old.IsViewActive);
        Assert.True(replacement.IsViewActive);
        Assert.Throws<InvalidOperationException>(() => view.Attach(old));
    }

    internal static ProbeGameWidget CreateWidget(CharacterPresentation view)
    {
        // Lifecycle tests exercise the actual widget entry points without initializing graphics assets.
        var widget = (ProbeGameWidget)RuntimeHelpers.GetUninitializedObject(typeof(ProbeGameWidget));
        typeof(GameWidget).GetField("<Presentation>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(widget, view);
        typeof(ContainerWidget).GetField(nameof(ContainerWidget.Children))!.SetValue(widget, new WidgetsList(widget));
        widget.GuiWidget = new CanvasWidget();
        widget.Children.Add(widget.GuiWidget);
        widget.IsVisible = true;
        widget.IsEnabled = true;
        return widget;
    }

    internal sealed class ProbeGameWidget(CharacterPresentation view) : GameWidget(view, 0)
    {
        public void MeasureForTest(Vector2 available) => MeasureOverride(available);
    }

    private sealed class CallbackWidget(Action update) : Widget
    {
        public override void Update() => update();
    }
}
