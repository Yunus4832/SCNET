using EntitySystem.TemplatesDatabase;

using Game.Configuration;
using Game.Network;
using Game.Network.Chunks;
using Game.Spawning;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class WorldPolicyBindingTest
{
    [Fact]
    public void FactoryCapturesEffectiveDefaultsWithoutIntroducingPolicyDefaults()
    {
        var settings = new HostPreferences();
        var policies = WorldProjectPolicies.Capture(settings);
        Assert.Equal(new CreatureSpawnPolicy(24, 3, 48, 3, 16, 18, 4, 2, 60f, 1f), policies.Spawn);
        Assert.Equal(NetworkTerrainPolicy.DefaultMaxClientVisibilityRange, policies.Interest.MaxClientVisibilityRange);
    }

    [Fact]
    public void DifferentWorldsKeepTheirOwnImmutableBudgetAndInterestSnapshots()
    {
        var settings = new HostPreferences
            { CreatureTotalLimit = 2, CreatureAreaLimit = 1, MaxClientVisibilityRange = 64 };
        var first = WorldProjectPolicies.Capture(settings);
        settings.CreatureTotalLimit = 70;
        settings.CreatureAreaLimit = 9;
        settings.CreatureSpawnIntervalTime = 4f;
        settings.MaxClientVisibilityRange = 256;
        var second = WorldProjectPolicies.Capture(settings);
        using var firstWorld = new RuntimeTestProject(RuntimeSide.Authority);
        using var secondWorld = new RuntimeTestProject(RuntimeSide.Authority);
        var firstSpawn = new SubsystemCreatureSpawn();
        var secondSpawn = new SubsystemCreatureSpawn();
        var firstInterest = new SubsystemNetworkInterest();
        var secondInterest = new SubsystemNetworkInterest();
        firstWorld.InitializeSubsystem(firstSpawn);
        firstWorld.InitializeSubsystem(firstInterest);
        secondWorld.InitializeSubsystem(secondSpawn);
        secondWorld.InitializeSubsystem(secondInterest);
        WorldProjectFactory.ConfigureWorldPolicies(firstSpawn, first.Spawn, first.Interest);
        WorldProjectFactory.ConfigureWorldPolicies(firstInterest, first.Spawn, first.Interest);
        WorldProjectFactory.ConfigureWorldPolicies(secondSpawn, second.Spawn, second.Interest);
        WorldProjectFactory.ConfigureWorldPolicies(secondInterest, second.Spawn, second.Interest);
        Assert.Equal(2, firstSpawn.Policy.TotalLimit);
        Assert.Equal(1, firstSpawn.Policy.AreaLimit);
        Assert.Equal(60f, firstSpawn.Policy.SpawnIntervalTime);
        Assert.Equal(64, firstInterest.Policy.MaxClientVisibilityRange);
        Assert.Equal(70, secondSpawn.Policy.TotalLimit);
        Assert.Equal(9, secondSpawn.Policy.AreaLimit);
        Assert.Equal(4f, secondSpawn.Policy.SpawnIntervalTime);
        Assert.Equal(256, secondInterest.Policy.MaxClientVisibilityRange);
    }

    [Fact]
    public void BothRealSubsystemsRequireExplicitPolicyBeforeLoadingDependencies()
    {
        var spawn = new SubsystemCreatureSpawn();
        var interest = new SubsystemNetworkInterest();
        Assert.Contains(
            "not configured",
            Assert.Throws<InvalidOperationException>(() => spawn.Load(new ValuesDictionary())).Message
        );
        Assert.Contains(
            "not configured",
            Assert.Throws<InvalidOperationException>(() => interest.Load(new ValuesDictionary())).Message
        );
    }

    [Fact]
    public void LoadedWorldPoliciesCannotBeReconfiguredIntoALiveGlobalSetting()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.AddSubsystem(new SubsystemGameInfo());
        project.AddSubsystem(new SubsystemSpawn());
        project.AddSubsystem(new SubsystemTerrain());
        project.AddSubsystem(new SubsystemTime());
        project.AddSubsystem(new SubsystemSky());
        project.AddSubsystem(new SubsystemBodies());
        project.AddSubsystem(new SubsystemCharacters());
        project.AddSubsystem(new SubsystemBlockEntities());
        var policies = WorldProjectPolicies.Capture(new HostPreferences());
        var spawn = new SubsystemCreatureSpawn();
        var interest = new SubsystemNetworkInterest();
        project.InitializeSubsystem(spawn);
        project.InitializeSubsystem(interest);
        WorldProjectFactory.ConfigureWorldPolicies(spawn, policies.Spawn, policies.Interest);
        WorldProjectFactory.ConfigureWorldPolicies(interest, policies.Spawn, policies.Interest);
        spawn.Load(new ValuesDictionary());
        interest.Load(new ValuesDictionary());
        Assert.Throws<InvalidOperationException>(() => spawn.ConfigurePolicy(policies.Spawn with { TotalLimit = 1 }));
        Assert.Throws<InvalidOperationException>(() => interest.ConfigurePolicy(new NetworkInterestPolicy(32)));
    }

    [Fact]
    public void SnapshotDoesNotAddNewRangesAndTheExistingNetworkClampStillDecidesBounds()
    {
        var policies = WorldProjectPolicies.Capture(
            new HostPreferences
            {
                CreatureTotalLimit = -3, CreatureAreaRadius = -1, MaxClientVisibilityRange = 0
            }
        );
        Assert.Equal(-3, policies.Spawn.TotalLimit);
        Assert.Equal(-1, policies.Spawn.AreaRadius);
        Assert.Equal(0, policies.Interest.MaxClientVisibilityRange);
        Assert.True(
            NetworkTerrainPolicy.TryClampClientUpdateLocation(
                new TerrainUpdateLocation
                {
                    VisibilityDistance = 256, ContentDistance = 512
                },
                policies.Interest.MaxClientVisibilityRange,
                default,
                out var actual
            )
        );
        Assert.Equal(32f, actual.VisibilityDistance);
        Assert.Equal(32f, actual.ContentDistance);
    }
}
