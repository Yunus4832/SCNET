using Engine.Core.Collections;

using Game.Terrains;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainMeshBufferCapacityTest
{
    [Fact]
    public void CompactionPreservesIndicesAndLeavesRoomForFurtherEdits()
    {
        var indices = new DynamicArray<int>(8192);
        for (var i = 0; i < 4096; i++) { indices.Add(i * 3); }

        Assert.True(TerrainMeshBuilder.TrimBufferCapacity(indices));
        Assert.Equal(4096, indices.Count);
        Assert.Equal(5120, indices.Capacity);
        Assert.Equal(Enumerable.Range(0, 4096).Select(i => i * 3), indices.ToArray());
        var storage = indices.Array;
        for (var i = 0; i < 1024; i++) { indices.Add(i); }

        Assert.Same(storage, indices.Array);
        Assert.False(TerrainMeshBuilder.TrimBufferCapacity(indices));
    }

    [Fact]
    public void ByteThresholdAvoidsCopyingSmallBuffers()
    {
        var small = new DynamicArray<int>(512) { Count = 128 };
        var storage = small.Array;
        Assert.False(TerrainMeshBuilder.TrimBufferCapacity(small));
        Assert.Same(storage, small.Array);
        var vertices = new DynamicArray<TerrainVertex>(512) { Count = 128 };
        Assert.True(TerrainMeshBuilder.TrimBufferCapacity(vertices));
        Assert.Equal(160, vertices.Capacity);
    }

    [Fact]
    public void ModerateCapacitySlackDoesNotTriggerCompaction()
    {
        var indices = new DynamicArray<int>(10000) { Count = 8000 };
        var storage = indices.Array;
        Assert.False(TerrainMeshBuilder.TrimBufferCapacity(indices));
        Assert.Same(storage, indices.Array);
    }

    [Fact]
    public void RemovedGeometryReleasesAnEmptyLargeBuffer()
    {
        var indices = new DynamicArray<int>(8192) { Count = 4096 };
        indices.Clear();
        Assert.True(TerrainMeshBuilder.TrimBufferCapacity(indices));
        Assert.Equal(0, indices.Capacity);
        Assert.Empty(indices);
        Assert.False(TerrainMeshBuilder.TrimBufferCapacity(indices));
    }
}
