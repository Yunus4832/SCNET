using Engine.Graphics;
using Engine.Media;

namespace Game.Automation;

public sealed record AutomationScreenshotResult(
    string Path,
    int Width,
    int Height);

public static class AutomationScreenshot
{
    public static AutomationScreenshotResult Capture()
    {
        var width = Display.Viewport.Width;
        var height = Display.Viewport.Height;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Display viewport is not initialized.");
        }

        using var target = new RenderTarget2D(
            width, height, 1, ColorFormat.Rgba8888, DepthFormat.Depth24Stencil8);
        var previousTarget = Display.RenderTarget;
        try
        {
            Display.RenderTarget = target;
            ScreensManager.Draw();
        }
        finally
        {
            Display.RenderTarget = previousTarget;
        }

        var filename = $"Automation-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png";
        var path = Storage.CombinePaths(GamePaths.ScreenCaptures, filename);
        if (!Storage.DirectoryExists(GamePaths.ScreenCaptures))
        {
            Storage.CreateDirectory(GamePaths.ScreenCaptures);
        }

        using var stream = Storage.OpenFile(path, OpenFileMode.Create);
        RenderTarget2D.Save(target, stream, ImageFileFormat.Png, saveAlpha: false);
        return new AutomationScreenshotResult(path, width, height);
    }
}
