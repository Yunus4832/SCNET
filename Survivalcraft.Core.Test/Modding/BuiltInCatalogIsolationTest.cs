using Game.Modding.Blocks;
using Game.Protocol;

namespace Survivalcraft.Core.Test.Modding;

public sealed class BuiltInCatalogIsolationTest
{
    [Fact]
    public void BuiltInFactoriesCreateIndependentRuleInstancesWithoutClientRuntime()
    {
        var registrations = BuiltInBlockRegistry.Entries.Select(entry =>
            new KeyValuePair<ResourceId, BlockRegistration>(
                new ResourceId(BuiltInBlockIdentities.Namespace, entry.Path),
                new BlockRegistration(entry.Factory)
            )
        ).ToArray();
        using var authority = new BlockContentCatalog(BlockRuntimeCatalog.Compile(registrations));
        using var replica = new BlockContentCatalog(BlockRuntimeCatalog.Compile(registrations));
        foreach (var entry in BuiltInBlockRegistry.Entries)
        {
            var id = new ResourceId(BuiltInBlockIdentities.Namespace, entry.Path);
            var authorityBlock = authority.GetBlock(id);
            var replicaBlock = replica.GetBlock(id);
            Assert.IsType(entry.BlockType, authorityBlock);
            Assert.IsType(entry.BlockType, replicaBlock);
            Assert.Equal(entry.Index, authorityBlock.BlockIndex);
            Assert.Equal(entry.Index, replicaBlock.BlockIndex);
            Assert.NotSame(authorityBlock, replicaBlock);
            Assert.Same(authority, authorityBlock.Catalog);
            Assert.Same(replica, replicaBlock.Catalog);
        }
    }
}
