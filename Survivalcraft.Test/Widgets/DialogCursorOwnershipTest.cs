using Engine.Core.Mathematics;

using Game.Widgets;

namespace Survivalcraft.Test.Widgets;

public sealed class DialogCursorOwnershipTest
{
    [Fact]
    public void DialogUiCanReleaseItsInputWithoutUpdatingTheWorld()
    {
        var input = new WidgetInput
        {
            UseSoftMouseCursor = true,
            IsMouseCursorVisible = false, IsPadCursorVisible = false, IsVrCursorVisible = false
        };
        var host = new CanvasWidget { WidgetsHierarchyInput = input };
        Widget.LayoutWidgetsHierarchy(host, new Vector2(400, 300));
        GameWidget.ReleaseDialogCursors(input, new Vector2(100, 200));
        Assert.True(input.IsMouseCursorVisible);
        Assert.Equal(new Vector2(100, 200), input.MousePosition);
    }
}
