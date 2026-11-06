namespace Game.Startup;

public static class StartupWindowModes
{
    public static WindowMode ToWindowMode(StartupWindowMode mode) => mode switch
    {
        StartupWindowMode.Resizable => WindowMode.Resizable,
        StartupWindowMode.Fixed => WindowMode.Fixed,
        StartupWindowMode.Borderless => WindowMode.Borderless,
        StartupWindowMode.Fullscreen => WindowMode.Fullscreen,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    public static StartupWindowMode FromWindowMode(WindowMode mode) => mode switch
    {
        WindowMode.Resizable => StartupWindowMode.Resizable,
        WindowMode.Fixed => StartupWindowMode.Fixed,
        WindowMode.Borderless => StartupWindowMode.Borderless,
        WindowMode.Fullscreen => StartupWindowMode.Fullscreen,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };
}
