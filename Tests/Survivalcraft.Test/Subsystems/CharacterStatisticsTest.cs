using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.TemplatesDatabase;

using Game.Characters;
using Game.Components;
using Game.Subsystems;

namespace Survivalcraft.Test.Subsystems;

public sealed class CharacterStatisticsTest
{
    [Fact]
    public void ReusedRuntimeIndicesAndRecreatedBodiesKeepEachCharactersStatistics()
    {
        var stats = new SubsystemCharacterStats();
        var firstId = Guid.NewGuid();
        var first = Character(stats, firstId, 0);
        var second = Character(stats, Guid.NewGuid(), 0);
        first.CharacterStats.BlocksDug = 17;
        second.CharacterStats.BlocksDug = 41;
        first.CharacterData.CharacterIndex = 3;
        Assert.Equal(17, first.CharacterStats.BlocksDug);
        Assert.Equal(41, second.CharacterStats.BlocksDug);
        var reconnected = Character(stats, firstId, 5);
        Assert.Same(first.CharacterStats, reconnected.CharacterStats);
        Assert.NotSame(first.CharacterStats, second.CharacterStats);
    }

    [Fact]
    public void CharacterStatsRoundTripAndInvalidReplacementDoesNotDestroyExistingData()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var stats = new SubsystemCharacterStats();
        stats.GetCharacterStats(first).BlocksDug = 17;
        stats.GetCharacterStats(second).BlocksDug = 41;
        var values = new ValuesDictionary();
        stats.Save(values);
        var loaded = new SubsystemCharacterStats();
        loaded.Load(values);
        Assert.Equal(17, loaded.GetCharacterStats(first).BlocksDug);
        Assert.Equal(41, loaded.GetCharacterStats(second).BlocksDug);
        foreach (var key in new[] { "0", Guid.Empty.ToString("N"), first.ToString("D") })
        {
            var records = new ValuesDictionary();
            records.SetValue(key, new ValuesDictionary());
            var invalid = new ValuesDictionary();
            invalid.SetValue("Stats", records);
            Assert.Throws<InvalidDataException>(() => loaded.Load(invalid));
            Assert.Equal(17, loaded.GetCharacterStats(first).BlocksDug);
        }

        Assert.Throws<ArgumentException>(() => loaded.GetCharacterStats(Guid.Empty));
    }

    private static ComponentCharacter Character(SubsystemCharacterStats stats, Guid characterId, int runtimeIndex)
    {
        var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        player.CharacterId = characterId;
        player.CharacterIndex = runtimeIndex;
        var component = new ComponentCharacter { CharacterData = player };
        typeof(ComponentCreature).GetField("subsystemCharacterStats", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(component, stats);
        return component;
    }
}
