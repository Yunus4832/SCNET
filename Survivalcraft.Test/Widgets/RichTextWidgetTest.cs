using Engine.Core;

using Game.Widgets;

namespace Survivalcraft.Test.Widgets;

public class RichTextWidgetTest
{
    [Fact]
    public void ResolvesNamedColorCaseInsensitively()
    {
        var resolved = RichTextWidget.TryResolveColor("ViOlEt", out var color);

        Assert.True(resolved);
        Assert.Equal(Color.Violet, color);
    }

    [Fact]
    public void ResolvesSerializedColorFromTagValue()
    {
        var resolved = RichTextWidget.TryResolveColor("255,0,0", out var color);

        Assert.True(resolved);
        Assert.Equal(Color.Red, color);
    }

    [Fact]
    public void RejectsInvalidColor()
    {
        var resolved = RichTextWidget.TryResolveColor("not-a-color", out var color);

        Assert.False(resolved);
        Assert.Equal(Color.White, color);
    }
}
