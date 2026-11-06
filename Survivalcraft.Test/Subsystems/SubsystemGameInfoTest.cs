using EntitySystem.TemplatesDatabase;

using Game.Hosting;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class SubsystemGameInfoTest
{
    [Theory]
    [InlineData(false, true, GameMode.Survival)]
    [InlineData(true, false, GameMode.Creative)]
    public void HostingContributesOnlyToStorageSnapshotWithoutChangingLiveRulesOrTemplate(
        bool network,
        bool expectedHosting,
        GameMode expectedMode
    )
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority) { SendToClientMode = network };
        var info = new SubsystemGameInfo
            { WorldSettings = new WorldSettings { Name = "World", GameMode = GameMode.Survival } };
        project.AddSubsystem(info);
        new WorldHostingSettings { RunServer = false }.Save(info.ValuesDictionary, "World");
        info.ValuesDictionary.SetValue("GameMode", GameMode.Survival);
        var owner = new SubsystemWorldHosting();
        project.AddSubsystem(owner);
        owner.Load(new ValuesDictionary());
        owner.ApplyRunServerOverride(true);
        owner.ApplyGameModeOverride(GameMode.Creative);
        var snapshot = project.Save();
        var saved = snapshot.ValuesDictionary.GetValue<ValuesDictionary>(info.ValuesDictionary.DatabaseObject.Name);
        Assert.Equal(expectedHosting, saved.ContainsKey("RunServer"));
        Assert.Equal(expectedHosting, saved.ContainsKey("P2P"));
        Assert.Equal(expectedMode, saved.GetValue<GameMode>("GameMode"));
        if (expectedHosting) { Assert.False(saved.GetValue<bool>("RunServer")); }

        Assert.True(owner.Settings.RunServer);
        Assert.Equal(GameMode.Creative, info.WorldSettings.GameMode);
        Assert.False(info.ValuesDictionary.GetValue<bool>("RunServer"));
        Assert.Equal(GameMode.Survival, info.ValuesDictionary.GetValue<GameMode>("GameMode"));
    }

    [Fact]
    public void SnapshotContributionFailurePreventsSaveAndDisposeRemovesHostingContribution()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var info = new SubsystemGameInfo { WorldSettings = new WorldSettings { Name = "World" } };
        project.AddSubsystem(info);
        var owner = new SubsystemWorldHosting();
        project.AddSubsystem(owner);
        owner.Load(new ValuesDictionary());
        owner.Dispose();
        owner.Dispose();
        var snapshot = project.Save();
        var saved = snapshot.ValuesDictionary.GetValue<ValuesDictionary>(info.ValuesDictionary.DatabaseObject.Name);
        Assert.False(saved.ContainsKey("RunServer"));
        project.AfterSubsystemsSaved += _ => throw new InvalidOperationException("Contribution failed");
        Assert.Throws<InvalidOperationException>(() => project.Save());
        Assert.Equal(GameMode.Survival, info.WorldSettings.GameMode);
    }
}
