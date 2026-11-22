namespace Game.Terrains.Distribution;

/// <summary>版本下限；缺失的增量必须用完整快照恢复。</summary>
public readonly record struct ChunkContentResync(ChunkAllocationId Allocation, long RequiredContentVersion);
