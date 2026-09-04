using Engine.Graphics;
using Engine.Media;

namespace Game.Screens;

public static class ScreenCaptureManager
{
    private static readonly string _screenshotDir = GamePaths.ScreenCaptures;

    private static bool _captureRequested;

    private static Action _successHandler = Actions.Empty;

    private static Action<Exception> _failureHandler = delegate { };

    public static void Run()
    {
        if (!_captureRequested)
        {
            return;
        }

        try
        {
            int num;
            int height;
            switch (SettingsManager.Current.ScreenshotSize)
            {
                case ScreenshotSize.ScreenSize:
                    {
                        num = MathUtils.Max(Window.ScreenSize.X, Window.ScreenSize.Y);
                        height = MathUtils.Min(Window.ScreenSize.X, Window.ScreenSize.Y);
                        var num2 = num / (float)height;
                        num = MathUtils.Min(num, 2048);
                        height = (int)MathUtils.Round(num / num2);
                        break;
                    }
                case ScreenshotSize.FullHD:
                    num = 1920;
                    height = 1080;
                    break;
                default:
                    num = 1920;
                    height = 1080;
                    break;
            }

            var now = DateTime.Now;
            Capture(num, height,
                $"Survivalcraft {now.Year:D4}-{now.Month:D2}-{now.Day:D2} {now.Hour:D2}-{now.Minute:D2}-{now.Second:D2}.jpg");
            _successHandler.Invoke();
            GC.Collect();
        }
        catch (Exception ex)
        {
            Log.Error($"Error capturing screen. Reason: {ex.Message}");
            _failureHandler.Invoke(ex);
        }
        finally
        {
            _captureRequested = false;
            _successHandler = Actions.Empty;
            _failureHandler = delegate { };
        }
    }

    public static void CapturePhoto(Action success, Action<Exception> failure)
    {
        if (_captureRequested)
        {
            return;
        }

        _captureRequested = true;
        _successHandler = success;
        _failureHandler = failure;
    }

    private static void Capture(int width, int height, string filename)
    {
        if (GameManager.Project is null)
        {
            throw new InvalidOperationException("GameManager.Project is not initialized");
        }

        using var renderTarget2D =
            new RenderTarget2D(width, height, 1, ColorFormat.Rgba8888, DepthFormat.Depth24Stencil8);
        var renderTarget = Display.RenderTarget;
        var dictionary = new Dictionary<ComponentGui, bool>();
        var resolutionMode = ResolutionMode.High;
        try
        {
            if (!SettingsManager.Current.ShowGuiInScreenshots)
            {
                foreach (var componentPlayer in GameManager.Project.FindSubsystem<SubsystemPlayers>(true)!
                             .ComponentPlayers)
                {
                    dictionary[componentPlayer.ComponentGui] =
                        componentPlayer.ComponentGui.ControlsContainerWidget.IsVisible;
                    componentPlayer.ComponentGui.ControlsContainerWidget.IsVisible = false;
                }
            }

            resolutionMode = SettingsManager.Current.ResolutionMode;
            SettingsManager.Current.ResolutionMode = ResolutionMode.High;
            Display.RenderTarget = renderTarget2D;
            ScreensManager.Draw();
            if (SettingsManager.Current.ShowLogoInScreenshots)
            {
                var primitivesRenderer2D = new PrimitivesRenderer2D();
                var texture2D = ContentManager.Get<Texture2D>("Textures/Gui/ScreenCaptureOverlay");
                var vector = new Vector2((width - texture2D.Width) / 2, 0f);
                var corner = vector + new Vector2(texture2D.Width, texture2D.Height);
                primitivesRenderer2D.TexturedBatch(texture2D, false, 0, DepthStencilState.None)
                    .QueueQuad(vector, corner, 0f, new Vector2(0f, 0f), new Vector2(1f, 1f), Color.White);
                primitivesRenderer2D.Flush();
            }
        }
        finally
        {
            Display.RenderTarget = renderTarget;
            foreach (var item in dictionary)
            {
                item.Key.ControlsContainerWidget.IsVisible = item.Value;
            }

            SettingsManager.Current.ResolutionMode = resolutionMode;
        }

        var image = new Image(renderTarget2D.Width, renderTarget2D.Height);
        renderTarget2D.GetData(image.Pixels, 0,
            new Rectangle(0, 0, renderTarget2D.Width, renderTarget2D.Height));
        if (!Storage.DirectoryExists(_screenshotDir))
        {
            Storage.CreateDirectory(_screenshotDir);
        }

        using var stream = Storage.OpenFile(Storage.CombinePaths(_screenshotDir, filename),
            OpenFileMode.CreateOrOpen);
        Image.Save(image, stream, ImageFileFormat.Jpg, false, sync: true);
    }
}
