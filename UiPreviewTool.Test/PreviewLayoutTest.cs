using Engine.Core;

using Game.Widgets;

namespace Scnet.UiPreviewTool.Test;

public class PreviewLayoutTest
{
    [Fact]
    public void ScaledViewportUsesLogicalLayoutAndPixelBounds()
    {
        using var root = new CanvasWidget
        {
            Name = "Preview.Viewport",
            Size = new Vector2(100, 100),
            LayoutTransform = Matrix.CreateScale(2)
        };
        root.Children.Add(new CanvasWidget { Size = new Vector2(50, 50) });
        PreviewLayout.Arrange(root, new Vector2(100, 100));
        Assert.Equal(200, root.GlobalBounds.Max.X);
        Assert.Equal(100, root.Children[0].GlobalBounds.Max.X);
        PreviewLayout.Describe(root, new Vector2(200, 200), out var errors, out var warnings);
        Assert.Equal(0, errors);
        Assert.Equal(0, warnings);
    }

    [Fact]
    public void OversizedRootIsReportedWhileExpectedScrollableClippingIsInformational()
    {
        using var viewport = new CanvasWidget { Name = "Preview.Viewport", Size = new Vector2(100, 100), ClampToBounds = true };
        var content = new CanvasWidget { Size = new Vector2(120, 120) };
        viewport.Children.Add(content);
        PreviewLayout.Arrange(viewport, new Vector2(100, 100));
        var report = PreviewLayout.Describe(viewport, new Vector2(100, 100), out var errors, out var warnings);
        Assert.Equal(0, errors);
        Assert.Equal(1, warnings);
        Assert.Contains(report.Descendants("Issue"), issue => (string?)issue.Attribute("Code") == "content-compressed");
        viewport.Children.Clear();
        var clip = new CanvasWidget { Size = new Vector2(80, 80), ClampToBounds = true };
        var overflow = new CanvasWidget { Size = new Vector2(80, 160) };
        clip.Children.Add(overflow);
        clip.SetWidgetPosition(overflow, Vector2.Zero);
        viewport.Children.Add(clip);
        PreviewLayout.Arrange(viewport, new Vector2(100, 100));
        report = PreviewLayout.Describe(viewport, new Vector2(100, 100), out errors, out warnings);
        Assert.Equal(0, errors);
        Assert.Equal(0, warnings);
        Assert.Contains(report.Descendants("Widget"), node => (bool?)node.Attribute("Clipped") == true);
    }
}
