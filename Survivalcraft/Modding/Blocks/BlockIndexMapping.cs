using System.Collections.ObjectModel;
using System.Xml.Linq;

namespace Game.Modding.Blocks;

public sealed class BlockIndexMapping
{
    public const int MaximumResourceIdLength = 256;

    public static BlockIndexMapping Empty { get; } = new([]);

    public BlockIndexMapping(IEnumerable<KeyValuePair<ResourceId, int>> entries)
    {
        var byId = new Dictionary<ResourceId, int>();
        var indices = new HashSet<int>();
        var reserved = BuiltInBlockRegistry.Entries.Select(entry => entry.Index).ToHashSet();
        foreach (var (id, index) in entries)
        {
            if (string.IsNullOrEmpty(id.Namespace.Value) || string.IsNullOrEmpty(id.Path) ||
                id.ToString().Length > MaximumResourceIdLength || id.Namespace == BuiltInContentMod.Manifest.ModId ||
                index is < 0 or >= BlockRuntimeCatalog.Capacity || reserved.Contains(index) ||
                !byId.TryAdd(id, index) || !indices.Add(index))
            {
                throw new InvalidDataException($"Invalid or duplicate block mapping: {id} -> {index}.");
            }
        }

        Entries = new ReadOnlyDictionary<ResourceId, int>(byId);
    }

    public IReadOnlyDictionary<ResourceId, int> Entries { get; }

    public static BlockIndexMapping Allocate(IEnumerable<ResourceId> definitions, BlockIndexMapping saved)
    {
        var result = new Dictionary<ResourceId, int>(saved.Entries);
        var used = BuiltInBlockRegistry.Entries.Select(entry => entry.Index).Concat(result.Values).ToHashSet();
        var next = 0;
        foreach (var id in definitions.OrderBy(id => id.ToString(), StringComparer.Ordinal))
        {
            if (id.Namespace == BuiltInContentMod.Manifest.ModId || result.ContainsKey(id))
            {
                continue;
            }

            while (next < BlockRuntimeCatalog.Capacity && used.Contains(next))
            {
                next++;
            }

            if (next == BlockRuntimeCatalog.Capacity)
            {
                throw new InvalidOperationException($"No block index is available for {id}.");
            }

            used.Add(next);
            result.Add(id, next);
        }

        return new BlockIndexMapping(result);
    }

    public XElement ToXml() => new("BlockRegistry", Entries.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
        .Select(pair => new XElement("Block", new XAttribute("Id", pair.Key), new XAttribute("Index", pair.Value))));

    public static BlockIndexMapping FromXml(XElement? element)
    {
        if (element is null)
        {
            return Empty;
        }

        return new BlockIndexMapping(element.Elements("Block").Select(block =>
            new KeyValuePair<ResourceId, int>(ResourceId.Parse((string?)block.Attribute("Id")
                ?? throw new InvalidDataException("Block mapping has no resource id.")),
                (int?)block.Attribute("Index") ?? throw new InvalidDataException("Block mapping has no index."))));
    }
}
