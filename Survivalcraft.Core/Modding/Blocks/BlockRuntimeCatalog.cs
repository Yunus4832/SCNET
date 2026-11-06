namespace Game.Modding.Blocks;

public sealed class BlockRuntimeCatalog
{
    public const int Capacity = BuiltInBlockIdentities.Capacity;

    private readonly IReadOnlyDictionary<ResourceId, BlockEntry> _byId;

    private readonly IReadOnlyList<BlockDataEntry> _dataEntries;

    private BlockRuntimeCatalog(
        IReadOnlyDictionary<ResourceId, BlockEntry> byId,
        IReadOnlyList<BlockDataEntry> dataEntries
    )
    {
        _byId = byId;
        _dataEntries = dataEntries;
    }

    public IReadOnlyDictionary<ResourceId, BlockEntry> ById => _byId;

    public IReadOnlyList<BlockDataEntry> DataEntries => _dataEntries;

    public BlockIndexMapping Mapping { get; private init; } = BlockIndexMapping.Empty;

    public static BlockRuntimeCatalog Compile(
        IReadOnlyList<KeyValuePair<ResourceId, BlockRegistration>> registrations,
        IReadOnlyList<BlockDataEntry>? blockData = null,
        BlockIndexMapping? savedMapping = null,
        bool requireExactMapping = false
    )
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var registry = registrations.ToDictionary(entry => entry.Key, entry => entry.Value);
        var byId = new Dictionary<ResourceId, BlockEntry>();
        var saved = savedMapping ?? BlockIndexMapping.Empty;
        var mapping = BlockIndexMapping.Allocate(registry.Keys, saved);
        if (requireExactMapping && mapping.Entries.Count != saved.Entries.Count)
        {
            throw new InvalidDataException("The server block mapping is incomplete.");
        }

        var fixedIndices = BuiltInBlockRegistry.Entries.ToDictionary(
            entry => new ResourceId(BuiltInBlockIdentities.Namespace, entry.Path),
            entry => entry.Index
        );
        var assigned = registry.Keys.ToDictionary(
            id => id,
            id =>
                id.Namespace == BuiltInBlockIdentities.Namespace
                    ? fixedIndices.TryGetValue(id, out var builtInIndex)
                        ? builtInIndex
                        : throw new InvalidOperationException($"Unknown built-in block {id}.")
                    : mapping.Entries[id]
        );
        if (assigned.Values.Any(index => index is < 0 or >= Capacity) ||
            assigned.Values.Distinct().Count() != assigned.Count)
        {
            throw new InvalidOperationException("The block registry contains conflicting or invalid indices.");
        }

        if (!assigned.ContainsKey(ResourceId.Parse("game:air")))
        {
            throw new InvalidOperationException("Block registry does not define air.");
        }

        try
        {
            foreach (var (id, registration) in
                     registry.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
            {
                var index = assigned[id];

                var block = registration.Factory();
                if (block.BlockIndex != -1)
                {
                    throw new InvalidOperationException(
                        $"Factory for {id} reused an already registered block instance."
                    );
                }

                block.BlockIndex = index;
                var entry = new BlockEntry(id, index, block);
                byId.Add(id, entry);
            }
        }
        catch
        {
            foreach (var entry in byId.Values)
            {
                try
                {
                    entry.Block.Dispose();
                }
                catch (Exception exception)
                {
                    Log.Error($"Could not dispose unbound block {entry.Id}: {exception}");
                }
            }

            throw;
        }

        var dataEntries = blockData?.OrderBy(entry => entry.Id.ToString(), StringComparer.Ordinal).ToArray() ?? [];
        return new BlockRuntimeCatalog(byId, dataEntries) { Mapping = mapping };
    }

    public bool TryGet(ResourceId id, out BlockEntry? entry) => _byId.TryGetValue(id, out entry);
}
