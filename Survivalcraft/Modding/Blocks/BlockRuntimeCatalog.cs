namespace Game.Modding.Blocks;

public sealed class BlockRuntimeCatalog
{
    public const int Capacity = 1024;
    private readonly IReadOnlyDictionary<ResourceId, BlockEntry> _byId;
    private readonly BlockEntry?[] _byIndex;
    private readonly IReadOnlyList<BlockDataEntry> _dataEntries;

    private BlockRuntimeCatalog(
        IReadOnlyDictionary<ResourceId, BlockEntry> byId,
        BlockEntry?[] byIndex,
        IReadOnlyList<BlockDataEntry> dataEntries)
    {
        _byId = byId;
        _byIndex = byIndex;
        _dataEntries = dataEntries;
    }

    public IReadOnlyDictionary<ResourceId, BlockEntry> ById => _byId;

    public IReadOnlyList<BlockDataEntry> DataEntries => _dataEntries;

    public static BlockRuntimeCatalog Compile(
        NamespacedRegistry<BlockRegistration> registry,
        NamespacedRegistry<BlockDataRegistration>? dataRegistry = null)
    {
        var byId = new Dictionary<ResourceId, BlockEntry>();
        var byIndex = new BlockEntry?[Capacity];
        foreach (var (id, registration) in
                 registry.Entries.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            if (registration.LegacyIndex is < 0 or >= Capacity)
            {
                throw new InvalidOperationException(
                    $"Block {id} uses index {registration.LegacyIndex}, outside the supported range 0-{Capacity - 1}.");
            }

            if (byIndex[registration.LegacyIndex] is { } conflict)
            {
                throw new InvalidOperationException(
                    $"Blocks {conflict.Id} and {id} both use runtime index {registration.LegacyIndex}.");
            }

            var block = registration.Factory();
            block.BlockIndex = registration.LegacyIndex;
            var entry = new BlockEntry(id, registration.LegacyIndex, block);
            byId.Add(id, entry);
            byIndex[registration.LegacyIndex] = entry;
        }

        if (byIndex[0] is null)
        {
            throw new InvalidOperationException("Block registry does not define runtime index 0.");
        }

        var dataEntries = dataRegistry?.Entries
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Select(pair => new BlockDataEntry(pair.Key, pair.Value.Read))
            .ToArray() ?? [];
        return new BlockRuntimeCatalog(byId, byIndex, dataEntries);
    }

    public bool TryGet(ResourceId id, out BlockEntry? entry) => _byId.TryGetValue(id, out entry);

    public Block[] CreateLegacyBlockArray()
    {
        var fallback = _byIndex[0]!.Block;
        var blocks = new Block[Capacity];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = _byIndex[index]?.Block ?? fallback;
        }

        return blocks;
    }
}

public sealed record BlockEntry(ResourceId Id, int RuntimeIndex, Block Block);

public sealed record BlockDataEntry(ResourceId Id, Func<string> Read);
