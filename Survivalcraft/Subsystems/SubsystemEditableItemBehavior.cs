using Engine.Serialization;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Subsystems;

public abstract class SubsystemEditableItemBehavior<T>(
    int contents
) : SubsystemBlockBehavior where T : IEditableItemData, new()
{
    private readonly Dictionary<Point3, T> _blocksData = new();

    public readonly Dictionary<int, T> ItemsData = new();

    private SubsystemItemsScanner _subsystemItemsScanner = null!;

    public T? GetBlockData(Point3 point)
    {
        _blocksData.TryGetValue(point, out var value);
        return value;
    }

    public void SetBlockData(Point3 point, T? t)
    {
        if (t != null)
        {
            _blocksData[point] = t;
        }
        else
        {
            _blocksData.Remove(point);
        }
    }

    public T? GetItemData(int id)
    {
        ItemsData.TryGetValue(id, out var value);
        return value;
    }

    public int StoreItemDataAtUniqueId(T t)
    {
        var num = FindFreeItemId();
        ItemsData[num] = t;
        return num;
    }

    public override void OnBlockPlaced(ComponentMiner miner, int x, int y, int z, ref BlockPlacementData placementData,
        int value)
    {
        var id = Terrain.ExtractData(value);
        var itemData = GetItemData(id);
        if (itemData != null)
        {
            _blocksData[new Point3(x, y, z)] = (T)itemData.Copy();
        }
    }

    public override void OnItemHarvested(int x, int y, int z, int blockValue, ref BlockDropValue dropValue,
        ref int newBlockValue)
    {
        var blockData = GetBlockData(new Point3(x, y, z));
        if (blockData != null)
        {
            var num = FindFreeItemId();
            ItemsData.Add(num, (T)blockData.Copy());
            dropValue.Value = Terrain.ReplaceData(dropValue.Value, num);
            if (CommonLib.WorkType != WorkType.Client)
            {
                if (blockData is MemoryBankData memory)
                {
                    CommonLib.Net.QueuePackage(new EditableBlockPackage(num, memory));
                }

                if (blockData is TruthTableData truthTableData)
                {
                    CommonLib.Net.QueuePackage(new EditableBlockPackage(num, truthTableData));
                }
            }
        }
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        _blocksData.Remove(new Point3(x, y, z));
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemItemsScanner = Project.FindSubsystem<SubsystemItemsScanner>(true)!;
        foreach (var item in valuesDictionary.GetValue<ValuesDictionary>("Blocks"))
        {
            var key = HumanReadableConverter.ConvertFromString<Point3>(item.Key);
            var value = new T();
            value.LoadString((string)item.Value);
            _blocksData[key] = value;
        }

        foreach (var item2 in valuesDictionary.GetValue<ValuesDictionary>("Items"))
        {
            var key2 = HumanReadableConverter.ConvertFromString<int>(item2.Key);
            var value2 = new T();
            value2.LoadString((string)item2.Value);
            ItemsData[key2] = value2;
        }

        _subsystemItemsScanner.ItemsScanned += GarbageCollectItems;
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        base.Save(valuesDictionary);
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Blocks", valuesDictionary2);
        foreach (var blocksDatum in _blocksData)
        {
            valuesDictionary2.SetValue(HumanReadableConverter.ConvertToString(blocksDatum.Key),
                blocksDatum.Value.SaveString());
        }

        var valuesDictionary3 = new ValuesDictionary();
        valuesDictionary.SetValue("Items", valuesDictionary3);
        foreach (var itemsDatum in ItemsData)
        {
            valuesDictionary3.SetValue(HumanReadableConverter.ConvertToString(itemsDatum.Key),
                itemsDatum.Value.SaveString());
        }
    }

    private int FindFreeItemId()
    {
        for (var i = 1; i < 1000; i++)
        {
            if (!ItemsData.ContainsKey(i))
            {
                return i;
            }
        }

        return 0;
    }

    private void GarbageCollectItems(ReadOnlyList<ScannedItemData> allExistingItems)
    {
        var hashSet = new HashSet<int>();
        foreach (var item in allExistingItems)
        {
            if (Terrain.ExtractContents(item.Value) == contents)
            {
                hashSet.Add(Terrain.ExtractData(item.Value));
            }
        }

        var list = new List<int>();
        foreach (var itemsDatum in ItemsData)
        {
            if (!hashSet.Contains(itemsDatum.Key))
            {
                list.Add(itemsDatum.Key);
            }
        }

        foreach (var item2 in list)
        {
            ItemsData.Remove(item2);
        }
    }
}
