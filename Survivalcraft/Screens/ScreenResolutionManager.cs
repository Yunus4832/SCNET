namespace Game.Screens;

public static class ScreenResolutionManager
{
    static ScreenResolutionManager()
    {
        // TODO: fix screen dpi
        ApproximateScreenDpi = 0.5f * (Window.ScreenSize.X / Window.Size.X + Window.ScreenSize.Y / Window.Size.Y);
        ApproximateScreenDpi = MathUtils.Clamp(ApproximateScreenDpi, 96f, 800f);
    }

    public static float ApproximateScreenDpi { get; set; }

    public static float ApproximateScreenInches =>
        MathUtils.Sqrt(Window.ScreenSize.X * Window.ScreenSize.X + Window.ScreenSize.Y * Window.ScreenSize.Y) /
        ApproximateScreenDpi;
}
