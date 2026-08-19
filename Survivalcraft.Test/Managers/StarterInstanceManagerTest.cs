using System.Xml.Linq;

using Engine.FileStorage;
using Engine.Core;

using Game.Managers;

namespace Game.Tests;

[Collection(Survivalcraft.Test.Modding.ConfigFileCollection.Name)]
public sealed class StarterInstanceManagerTest : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"scnet-starter-{Guid.NewGuid():N}");

    public StarterInstanceManagerTest()
    {
        Directory.CreateDirectory(_directory);
        Storage.RegisterFileSystemRoot("starter", _directory);
    }

    [Fact]
    public void InitializeCreatesDefaultInstanceAndSettings()
    {
        var context = StarterInstanceManager.Initialize([]);

        Assert.Equal("default", context.Id);
        Assert.Equal(context, StarterInstanceManager.Current);
        Assert.Empty(context.GameArguments);
        Assert.True(Directory.Exists(Path.Combine(_directory, "Instances", "default")));
        var settings = XElement.Load(Path.Combine(_directory, "Starter.xml"));
        Assert.Equal("default", settings.Attribute("CurrentInstance")?.Value);
        Assert.Equal(string.Empty, settings.Attribute("NextInstance")?.Value);
    }

    [Fact]
    public void InstanceArgumentHasPriorityAndIsRemovedFromGameArguments()
    {
        File.WriteAllText(
            Path.Combine(_directory, "Starter.xml"),
            "<Starter CurrentInstance=\"default\" NextInstance=\"pending\" />");

        var context = StarterInstanceManager.Initialize(
            ["--server", "--instance", "debug_server", "--session", "smoke"]);

        Assert.Equal("debug_server", context.Id);
        Assert.Equal("debug_server", StarterInstanceManager.Current.Id);
        Assert.Equal(["--server", "--session", "smoke"], context.GameArguments);
        Assert.True(Directory.Exists(Path.Combine(_directory, "Instances", "debug_server")));
    }

    [Fact]
    public void PendingInstanceIsConsumedAndBecomesCurrent()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "Instances", "next"));
        File.WriteAllText(
            Path.Combine(_directory, "Starter.xml"),
            "<Starter CurrentInstance=\"default\" NextInstance=\"next\" />");

        var context = StarterInstanceManager.Initialize([]);

        Assert.Equal("next", context.Id);
        var settings = XElement.Load(Path.Combine(_directory, "Starter.xml"));
        Assert.Equal("next", settings.Attribute("CurrentInstance")?.Value);
        Assert.Equal(string.Empty, settings.Attribute("NextInstance")?.Value);
    }

    [Fact]
    public void RequestSwitchRequiresAnExistingInstance()
    {
        StarterInstanceManager.Initialize(["--instance", "target"]);
        StarterInstanceManager.RequestSwitch("target");

        var settings = XElement.Load(Path.Combine(_directory, "Starter.xml"));
        Assert.Equal("target", settings.Attribute("NextInstance")?.Value);
        Assert.Throws<InvalidOperationException>(() => StarterInstanceManager.RequestSwitch("missing"));
    }

    [Fact]
    public void GetRunModeReadsTheTargetInstanceSettings()
    {
        StarterInstanceManager.Initialize(["--instance", "server"]);
        var configDirectory = Path.Combine(_directory, "Instances", "server", "Config");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, "RunningSetting.xml"),
            "<RunningSetting RunMode=\"HeadlessServer\" />");

        Assert.Equal(RunModeType.HeadlessServer, StarterInstanceManager.GetRunMode("server"));
        Assert.Equal(RunModeType.Gui, StarterInstanceManager.GetRunMode("default"));
    }

    [Fact]
    public void CreateAndDeleteManageNonCurrentInstanceDirectories()
    {
        StarterInstanceManager.Initialize([]);

        StarterInstanceManager.CreateInstance("secondary");
        var instanceDirectory = Path.Combine(_directory, "Instances", "secondary");
        Directory.CreateDirectory(Path.Combine(instanceDirectory, "Config"));
        File.WriteAllText(Path.Combine(instanceDirectory, "Config", "value.txt"), "data");

        Assert.Contains("secondary", StarterInstanceManager.ListInstances());
        Assert.Throws<InvalidOperationException>(() => StarterInstanceManager.CreateInstance("secondary"));
        Assert.Throws<InvalidOperationException>(() => StarterInstanceManager.DeleteInstance("default"));

        StarterInstanceManager.DeleteInstance("secondary");

        Assert.False(Directory.Exists(instanceDirectory));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("with/slash")]
    [InlineData("with space")]
    [InlineData("")]
    public void InvalidInstanceIdsAreRejected(string instanceId)
    {
        Assert.ThrowsAny<ArgumentException>(() => StarterInstanceManager.ValidateInstanceId(instanceId));
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }
}
