using Game.Modding.Blocks;
using Game.Protocol;

namespace Survivalcraft.Core.Test.Modding;

public sealed class BlockIndexMappingTest
{
    [Fact]
    public void BuiltInIdentitiesMatchTheCompleteBaselineAndAreUnique()
    {
        var expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "BuiltInBlocks.txt"))
            .Select(line => string.Join(';', line.Split(';').Take(2))).ToArray();
        var actual = BuiltInBlockIdentities.Entries
            .Select(entry => $"{entry.Index};{BuiltInBlockIdentities.Namespace}:{entry.Path}").ToArray();
        Assert.Equal(expected, actual);
        Assert.Equal(actual.Length, BuiltInBlockIdentities.Entries.Select(entry => entry.Index).Distinct().Count());
        Assert.Equal(actual.Length, BuiltInBlockIdentities.Entries.Select(entry => entry.Path).Distinct().Count());
    }

    [Fact]
    public void AllocationPreservesSavedIdentitiesAndRoundTripsThroughXml()
    {
        var first = ResourceId.Parse("test:first");
        var second = ResourceId.Parse("test:second");
        var saved = new BlockIndexMapping([new KeyValuePair<ResourceId, int>(first, 1000)]);
        var mapping = BlockIndexMapping.Allocate([second, first], saved);
        var restored = BlockIndexMapping.FromXml(mapping.ToXml());
        Assert.Equal(1000, restored.Entries[first]);
        Assert.Equal(mapping.Entries[second], restored.Entries[second]);
        Assert.Equal(2, restored.Entries.Count);
        Assert.DoesNotContain(BuiltInBlockIdentities.Entries, entry => entry.Index == restored.Entries[second]);
    }

    [Fact]
    public void MappingRejectsReservedIndicesAndBuiltInNamespace()
    {
        Assert.Throws<InvalidDataException>(() => new BlockIndexMapping(
                [new KeyValuePair<ResourceId, int>(ResourceId.Parse("test:air"), 0)]
            )
        );
        Assert.Throws<InvalidDataException>(() => new BlockIndexMapping(
                [new KeyValuePair<ResourceId, int>(ResourceId.Parse("game:custom"), 1000)]
            )
        );
    }
}
