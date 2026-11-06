using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Characters;

namespace Survivalcraft.Test.Characters;

public sealed class CombatStatisticsReplicationTest
{
    [Fact]
    public void SurvivalSnapshotPreservesCombatHistoryAndMovementAndIsIdempotent()
    {
        var client = new CharacterStats { PlayerKills = 3, DistanceWalked = 123 };
        client.AddDeathRecord(
            new CharacterStats.DeathRecord { Day = 1, Location = Vector3.One, Cause = LocalizedText.Literal("Fall") }
        );
        var authority = new CharacterStats { TimesPuked = 2, TimesWasSick = 1, TimeSlept = 12 };
        var snapshot = new ValuesDictionary();
        authority.SaveSurvival(snapshot);
        var received = new ValuesDictionary();
        received.ApplyOverridesUseMessagePack(snapshot.ToMessagePack());
        client.ApplySnapshot(received);
        client.ApplySnapshot(received);
        Assert.Equal(2, client.TimesPuked);
        Assert.Equal(1, client.TimesWasSick);
        Assert.Equal(12, client.TimeSlept);
        Assert.Equal(3, client.PlayerKills);
        Assert.Equal(123, client.DistanceWalked);
        Assert.Equal(LocalizedText.Literal("Fall"), Assert.Single(client.DeathRecords).Cause);
    }

    [Fact]
    public void AuthoritativeCombatSnapshotIsIdempotentAndPreservesLocallyMaintainedCounters()
    {
        var authority = new CharacterStats
        {
            HitsReceived = 3, TotalHealthLost = .8, LandCreatureKills = 2, PlayerKills = 1, MeleeAttacks = 7,
            MeleeHits = 4
        };
        authority.AddDeathRecord(
            new CharacterStats.DeathRecord { Day = 2, Location = Vector3.One, Cause = LocalizedText.Literal("Fall") }
        );
        var snapshot = new ValuesDictionary();
        authority.SaveCombat(snapshot);
        var wire = snapshot.ToMessagePack();
        var client = new CharacterStats { DistanceWalked = 123, Jumps = 7 };
        for (var i = 0; i < 2; i++)
        {
            var received = new ValuesDictionary();
            received.ApplyOverridesUseMessagePack(wire);
            client.Load(received);
            Assert.Equal(3, client.HitsReceived);
            Assert.Equal(7, client.MeleeAttacks);
            Assert.Equal(4, client.MeleeHits);
            Assert.Equal(.8, client.TotalHealthLost);
            Assert.Equal(2, client.LandCreatureKills);
            Assert.Equal(1, client.PlayerKills);
            Assert.Equal(LocalizedText.Literal("Fall"), Assert.Single(client.DeathRecords).Cause);
            Assert.Equal(123, client.DistanceWalked);
            Assert.Equal(7, client.Jumps);
        }
    }
}
