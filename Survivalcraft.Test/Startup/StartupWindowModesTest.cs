using Engine.Windowing;

using Game.Startup;

namespace Survivalcraft.Test.Startup;

public class StartupWindowModesTest
{
    [Theory]
    [InlineData(StartupWindowMode.Resizable, WindowMode.Resizable)]
    [InlineData(StartupWindowMode.Fixed, WindowMode.Fixed)]
    [InlineData(StartupWindowMode.Borderless, WindowMode.Borderless)]
    [InlineData(StartupWindowMode.Fullscreen, WindowMode.Fullscreen)]
    public void StartupModesMapAtTheWindowBoundary(StartupWindowMode startup, WindowMode backend)
    {
        Assert.Equal(backend, StartupWindowModes.ToWindowMode(startup));
        Assert.Equal(startup, StartupWindowModes.FromWindowMode(backend));
    }

    [Fact]
    public void UndefinedModesDoNotSilentlySelectAnotherWindowMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StartupWindowModes.ToWindowMode((StartupWindowMode)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => StartupWindowModes.FromWindowMode((WindowMode)99));
    }
}
