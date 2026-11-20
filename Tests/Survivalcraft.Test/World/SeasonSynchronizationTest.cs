using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.World;

[Collection(ConfigFileCollection.Name)]
public sealed class SeasonSynchronizationTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority, 1)]
    [InlineData(RuntimeSide.Client, 0)]
    public void OnlyAuthorityRequestsSynchronizationAtTheExistingTenSecondBoundary(RuntimeSide side, int expected)
    {
        using var project = new RuntimeTestProject(side);
        var settings = new WorldSettings { TimeOfYear = 0.625f };
        project.AddSubsystem(new SubsystemGameInfo { WorldSettings = settings });
        var seasons = new SubsystemSeasons();
        project.InitializeSubsystem(seasons);
        seasons.Load(new ValuesDictionary());
        Assert.Equal(default, seasons.Season);
        Assert.Equal(0f, seasons.TimeOfSeason);
        var sent = new List<float>();
        seasons.TimeOfYearSynchronizationRequested += sent.Add;
        var clock = WorldRuntimeContext.For(project).Clock;
        clock.Advance(.016f, 9.99);
        seasons.Update(0.016f);
        Assert.Empty(sent);
        seasons.SetTimeOfYear(1.375f);
        Assert.Equal(0.375f, settings.TimeOfYear);
        Assert.Equal(Season.Autumn, seasons.Season);
        Assert.Equal(0.5f, seasons.TimeOfSeason);
        Assert.Empty(sent);
        clock.Advance(.016f, 10.0);
        seasons.Update(0.016f);
        Assert.Equal(expected, sent.Count);
        Assert.All(sent, year => Assert.Equal(0.375f, year));
        clock.Advance(.016f, 10.02);
        seasons.Update(0.016f);
        Assert.Equal(expected, sent.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => seasons.SetTimeOfYear(float.NaN));
        Assert.Equal(0.375f, settings.TimeOfYear);
    }
}
