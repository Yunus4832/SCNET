using System.Text;
using System.Xml.Linq;

using Engine.Core.Logging;

using Game.Startup;

namespace Survivalcraft.Hosting.Test.Startup;

public class RunningSettingSerializerTest
{
    [Fact]
    public void MissingAndInvalidValuesRetainStartupDefaults()
    {
        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(
                "<RunningSetting LogLevel='invalid' WindowMode='invalid' WindowWidth='-10' WindowHeight='bad' />"
            )
        );
        var settings = RunningSettingSerializer.Load(stream);

        Assert.Equal(LogType.Information, settings.LogLevel);
        Assert.Equal(StartupWindowMode.Resizable, settings.WindowMode);
        Assert.Equal(0, settings.WindowWidth);
        Assert.Equal(0, settings.WindowHeight);
        Assert.Empty(settings.DefaultSessionId);
        Assert.Empty(settings.PendingSessionId);
        Assert.Empty(settings.RemainingArgs);
    }

    [Fact]
    public void RoundTripPreservesSettingsAndNormalizesSessionReferences()
    {
        var settings = new RunningSetting
        {
            LogLevel = LogType.Debug,
            WindowMode = StartupWindowMode.Borderless,
            WindowWidth = 1280,
            WindowHeight = 720,
            DefaultSessionId = " default ",
            PendingSessionId = " pending ",
            RemainingArgs = ["--custom", "a<&b", ""]
        };
        using var stream = new MemoryStream();
        RunningSettingSerializer.Save(stream, settings);
        Assert.True(stream.CanWrite);
        stream.Position = 0;
        var root = XElement.Load(stream);
        Assert.Equal("Borderless", root.Attribute("WindowMode")?.Value);
        stream.Position = 0;
        var loaded = RunningSettingSerializer.Load(stream);

        Assert.Equal(settings.LogLevel, loaded.LogLevel);
        Assert.Equal(settings.WindowMode, loaded.WindowMode);
        Assert.Equal(1280, loaded.WindowWidth);
        Assert.Equal(720, loaded.WindowHeight);
        Assert.Equal("default", loaded.DefaultSessionId);
        Assert.Equal("pending", loaded.PendingSessionId);
        Assert.Equal(settings.RemainingArgs, loaded.RemainingArgs);
    }

    [Theory]
    [InlineData("fullscreen", StartupWindowMode.Fullscreen)]
    [InlineData("2", StartupWindowMode.Borderless)]
    public void ExistingWindowNamesAndNumericValuesRemainReadable(string value, StartupWindowMode expected)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"<RunningSetting WindowMode='{value}' />"));
        Assert.Equal(expected, RunningSettingSerializer.Load(stream).WindowMode);
    }

    [Fact]
    public void MalformedXmlIsReportedToTheHost()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<RunningSetting"));
        Assert.Throws<System.Xml.XmlException>(() => RunningSettingSerializer.Load(stream));
    }
}
