using System.Xml.Linq;

using Engine.Core.Serialization;

using Game.Blocks.Collision;

namespace Game.Modding.Blocks;

public sealed class BlockContentCatalog : IDisposable
{
    private readonly Dictionary<int, ResourceId> _blockIdsByIndex = [];

    private readonly List<Block> _registeredBlocks = [];

    private readonly Dictionary<ResourceId, List<BlockAssetValue>> _assetValues = new();

    public IReadOnlyList<BlockAssetValue> GetAssetValues(ResourceId id) =>
        _assetValues.TryGetValue(id, out var values) ? values : [];

    public CollisionGeometryCatalog CollisionGeometry { get; private set; } = new([]);

    public Block[] Blocks { get; private set; } = new Block[1024];

    public FluidBlock?[] FluidBlocks { get; private set; } = new FluidBlock[1024];


    public IReadOnlyList<Block> RegisteredBlocks => _registeredBlocks;

    private bool _disposed;

    private bool _initialized;

    public BlockRuntimeCatalog Registry { get; }


    public CraftingRecipeCatalog Recipes { get; }

    public BlockContentCatalog(BlockRuntimeCatalog registry)
    {
        Registry = registry;
        Recipes = new CraftingRecipeCatalog(this);
        foreach (var entry in registry.ById.Values
                     .OrderBy(entry => entry.Id.Namespace != BuiltInBlockIdentities.Namespace))
        {
            entry.Block.Catalog = this;
            Blocks[entry.RuntimeIndex] = entry.Block;
            _registeredBlocks.Add(entry.Block);
            _blockIdsByIndex.Add(entry.RuntimeIndex, entry.Id);
            if (entry.Block is FluidBlock fluid)
            {
                FluidBlocks[entry.RuntimeIndex] = fluid;
            }
        }

        var air = Blocks[0] ?? throw new InvalidOperationException("The catalog has no air block.");
        for (var index = 0; index < Blocks.Length; index++)
        {
            Blocks[index] ??= air;
        }
    }

    public void Initialize(CollisionGeometryCatalog collisionGeometry, XElement? clothingData, XElement recipes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            throw new InvalidOperationException("This content catalog is already initialized.");
        }

        ArgumentNullException.ThrowIfNull(collisionGeometry);
        ArgumentNullException.ThrowIfNull(recipes);
        CollisionGeometry = collisionGeometry;
        try
        {
            if (clothingData is not null)
            {
                foreach (var clothing in _registeredBlocks.OfType<ClothingBlock>())
                {
                    clothing.SetInitializationData(clothingData);
                }
            }

            foreach (var dataEntry in Registry.DataEntries)
            {
                LoadBlocksData(dataEntry.Data);
            }

            foreach (var block in _registeredBlocks)
            {
                block.Initialize();
                if (string.IsNullOrEmpty(block.CraftingId))
                {
                    block.CraftingId = block.GetType().Name;
                }
            }

            Recipes.Initialize(recipes);
            _initialized = true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public IEnumerable<int> GetCreativeValues()
    {
        return _registeredBlocks
            .SelectMany(block => block.GetCreativeValues());
    }

    public bool TryGetBlockId(int runtimeIndex, out ResourceId id)
    {
        return _blockIdsByIndex.TryGetValue(runtimeIndex, out id);
    }

    public Block[] FindBlocksByCraftingId(string craftingId)
    {
        return _registeredBlocks.Where(c => c.MatchCraftingId(craftingId)).ToArray();
    }

    // 新增高度渲染cube
    public int DamageItem(int value, int damageCount)
    {
        var num = Terrain.ExtractContents(value);
        var block = Blocks[num];
        if (block.Durability >= 0)
        {
            var num2 = block.GetDamage(value) + damageCount;
            if (num2 <= block.Durability)
            {
                return block.SetDamage(value, num2);
            }

            return block.GetDamageDestructionValue(value);
        }

        return value;
    }

    private void LoadBlocksData(string data)
    {
        data = data.Replace("\r", string.Empty);
        var blockDataArray = data.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
        if (blockDataArray.Length == 0) { throw new InvalidDataException("Block data has no header."); }

        var firstDataContentArray = blockDataArray[0].Split(';');
        var restDataArray = new string[firstDataContentArray.Length - 1];
        Array.Copy(
            sourceArray: firstDataContentArray,
            sourceIndex: 1,
            destinationArray: restDataArray,
            destinationIndex: 0,
            length: firstDataContentArray.Length - 1
        );
        var ruleColumns = _registeredBlocks.SelectMany(block => block.GetType().GetRuntimeFields())
            .Where(field => field.IsPublic && !field.IsStatic).Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var column in restDataArray)
        {
            if (!ruleColumns.Contains(column) && !BlockAssetValues.Columns.ContainsKey(column))
            {
                throw new InvalidDataException($"Unknown block data column {column}.");
            }
        }

        for (var i = 1; i < blockDataArray.Length; i++)
        {
            if (string.IsNullOrEmpty(blockDataArray[i]))
            {
                continue;
            }

            var dataContentArray = blockDataArray[i].Split(';');

            if (dataContentArray.Length != restDataArray.Length + 1)
            {
                throw new InvalidDataException(
                    $"Block data row {i + 1} for '{dataContentArray[0]}' has {dataContentArray.Length} values; expected {restDataArray.Length + 1}."
                );
            }

            var typeName = dataContentArray[0];
            if (string.IsNullOrEmpty(typeName))
            {
                continue;
            }

            var resourceId = ResourceId.Parse(typeName);
            var block = GetBlock(resourceId);
            var fieldInfoDict = block.GetType().GetRuntimeFields()
                .Where(runtimeField => runtimeField is { IsPublic: true, IsStatic: false })
                .ToDictionary(runtimeField => runtimeField.Name);

            for (var j = 1; j < dataContentArray.Length; j++)
            {
                var text = restDataArray[j - 1];
                var text2 = dataContentArray[j];
                if (string.IsNullOrEmpty(text2))
                {
                    continue;
                }

                if (BlockAssetValues.Columns.TryGetValue(text, out var assetType))
                {
                    HumanReadableConverter.ConvertFromString(assetType, text2);
                    if (!_assetValues.TryGetValue(resourceId, out var values))
                    {
                        values = [];
                        _assetValues.Add(resourceId, values);
                    }

                    values.Add(new BlockAssetValue(text, text2));
                    continue;
                }

                if (!fieldInfoDict.TryGetValue(text, out var value))
                {
                    throw new InvalidDataException($"Block {resourceId} has no data field {text}.");
                }

                object? obj;
                if (text2.StartsWith('#'))
                {
                    var refTypeName = text2[1..];
                    obj = !string.IsNullOrEmpty(refTypeName)
                        ? GetBlock(ResourceId.Parse(refTypeName)).BlockIndex
                        : (object)block.BlockIndex;
                }
                else
                {
                    obj = HumanReadableConverter.ConvertFromString(value.FieldType, text2);
                }

                value.SetValue(block, obj);
            }
        }
    }


    public Block GetBlock(ResourceId id) => Registry.TryGet(id, out var entry)
        ? entry!.Block
        : throw new KeyNotFoundException($"Block {id} is not registered in this catalog.");

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var block in _registeredBlocks)
        {
            try
            {
                block.Dispose();
            }
            catch (Exception exception)
            {
                Log.Error($"Could not dispose block {block.GetType().FullName}: {exception}");
            }
        }
    }
}
