using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

/// <summary>
/// 基础包模板复制
/// </summary>
public class EditableBlockPackage : IPackage
{
    private CellFace _cellFace;

    private byte[] _data = [];

    private int _delay;

    private bool _editAsItem;

    private int? _id;

    private int _inventoryId;

    private EditableItemType _itemType;

    private int _slotIndex;

    private bool _syncItem;

    public byte ID => (byte)PackageType.EditableBlock;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public EditableBlockPackage()
    {
    }

    public EditableBlockPackage(
        EditableItemType itemType,
        CellFace cell,
        bool editAsItem,
        int inventoryId,
        int slotIndex,
        int delay
    )
    {
        _itemType = itemType;
        _inventoryId = inventoryId;
        _cellFace = cell;
        _editAsItem = editAsItem;
        _slotIndex = slotIndex;
        _delay = delay;
    }

    public EditableBlockPackage(
        CellFace cell,
        bool editAsItem,
        int inventoryId,
        int slotIndex,
        TruthTableData truthTableData
    )
    {
        _itemType = EditableItemType.TruthTable;
        _cellFace = cell;
        _editAsItem = editAsItem;
        _inventoryId = inventoryId;
        _slotIndex = slotIndex;
        _data = truthTableData.Data.ToArray();
    }

    public EditableBlockPackage(
        CellFace cell,
        bool editAsItem,
        int inventoryId,
        int slotIndex,
        MemoryBankData memoryBankData
    )
    {
        _itemType = EditableItemType.MemoryBank;
        _cellFace = cell;
        _editAsItem = editAsItem;
        _inventoryId = inventoryId;
        _slotIndex = slotIndex;
        _data = memoryBankData.Data.ToArray();
    }

    public EditableBlockPackage(int id, TruthTableData truthTableData)
    {
        _itemType = EditableItemType.TruthTable;
        _data = truthTableData.Data.ToArray();
        _slotIndex = id;
        _syncItem = true;
    }

    public EditableBlockPackage(int id, MemoryBankData memoryBankData)
    {
        _itemType = EditableItemType.MemoryBank;
        _data = memoryBankData.Data.ToArray();
        _syncItem = true;
        _slotIndex = id;
    }


    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subInventory = project.FindSubsystem<SubsystemInventories>(true)!;
        switch (_itemType)
        {
            case EditableItemType.MemoryBank:
                var behavior = project.FindSubsystem<SubsystemMemoryBankBlockBehavior>();
                if (behavior is null)
                {
                    return;
                }

                var data = new MemoryBankData();
                data.Data.AddRange(_data);
                if (_syncItem)
                {
                    behavior.ItemsData[_slotIndex] = data;
                }
                else
                {
                    if (_editAsItem)
                    {
                        subInventory.FindInventoryById(_inventoryId, inventory =>
                        {
                            if (!_id.HasValue)
                            {
                                _id = behavior.StoreItemDataAtUniqueId(data);
                                ReplaceDataAtSlot(inventory, _slotIndex, _ => _id.Value);
                            }
                            else
                            {
                                behavior.ItemsData[_id.Value] = data;
                            }
                        });
                    }
                    else
                    {
                        behavior.SetBlockData(_cellFace.Point, data);
                    }
                }

                break;
            case EditableItemType.TruthTable:
                var truthBehavior = project.FindSubsystem<SubsystemTruthTableCircuitBlockBehavior>(true)!;
                var truthData = new TruthTableData
                {
                    Data = _data
                };
                if (_syncItem)
                {
                    truthBehavior.ItemsData[_slotIndex] = truthData;
                }
                else
                {
                    if (_editAsItem)
                    {
                        subInventory.FindInventoryById(_inventoryId, inventory =>
                        {
                            if (!_id.HasValue)
                            {
                                _id = truthBehavior.StoreItemDataAtUniqueId(truthData);
                                ReplaceDataAtSlot(inventory, _slotIndex, _ => _id.Value);
                            }
                            else
                            {
                                truthBehavior.ItemsData[_id.Value] = truthData;
                            }
                        });
                    }
                    else
                    {
                        truthBehavior.SetBlockData(_cellFace.Point, truthData);
                    }
                }

                break;
            case EditableItemType.AdjustableDelayGate:
                if (_editAsItem)
                {
                    subInventory.FindInventoryById(_inventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(inventory, _slotIndex,
                                d => AdjustableDelayGateBlock.SetDelay(d, _delay));
                        });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(_cellFace.X, _cellFace.Y, _cellFace.Z);
                    var newValue = Terrain.ReplaceData(value,
                        AdjustableDelayGateBlock.SetDelay(Terrain.ExtractData(value), _delay));
                    st.ChangeCell(_cellFace.X, _cellFace.Y, _cellFace.Z, newValue);
                }

                break;
            case EditableItemType.Battery:
                if (_editAsItem)
                {
                    subInventory.FindInventoryById(_inventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(inventory, _slotIndex, d => BatteryBlock.SetVoltageLevel(d, _delay));
                        });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(_cellFace.X, _cellFace.Y, _cellFace.Z);
                    var newValue = Terrain.ReplaceData(value,
                        BatteryBlock.SetVoltageLevel(Terrain.ExtractData(value), _delay));
                    st.ChangeCell(_cellFace.X, _cellFace.Y, _cellFace.Z, newValue);
                }

                break;
            case EditableItemType.Switch:
                if (_editAsItem)
                {
                    subInventory.FindInventoryById(_inventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(inventory, _slotIndex, d => SwitchBlock.SetVoltageLevel(d, _delay));
                        });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(_cellFace.X, _cellFace.Y, _cellFace.Z);
                    var newValue = Terrain.ReplaceData(value,
                        SwitchBlock.SetVoltageLevel(Terrain.ExtractData(value), _delay));
                    st.ChangeCell(_cellFace.X, _cellFace.Y, _cellFace.Z, newValue);
                }

                break;
            case EditableItemType.Button:
                if (_editAsItem)
                {
                    subInventory.FindInventoryById(_inventoryId,
                        inventory =>
                        {
                            ReplaceDataAtSlot(inventory, _slotIndex, d => ButtonBlock.SetVoltageLevel(d, _delay));
                        });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(_cellFace.X, _cellFace.Y, _cellFace.Z);
                    var newValue = Terrain.ReplaceData(value,
                        ButtonBlock.SetVoltageLevel(Terrain.ExtractData(value), _delay));
                    st.ChangeCell(_cellFace.X, _cellFace.Y, _cellFace.Z, newValue);
                }

                break;
            case EditableItemType.Piston:
                if (_editAsItem)
                {
                    subInventory.FindInventoryById(_inventoryId,
                        inventory => { ReplaceDataAtSlot(inventory, _slotIndex, _ => _delay); });
                }
                else
                {
                    var st = project.FindSubsystem<SubsystemTerrain>(true)!;
                    var value = st.Terrain.GetCellValue(_cellFace.X, _cellFace.Y, _cellFace.Z);
                    var newValue = Terrain.ReplaceData(value, _delay);
                    st.ChangeCell(_cellFace.X, _cellFace.Y, _cellFace.Z, newValue);
                }

                break;
        }

        if (isServer)
        {
            netNode.QueuePackage(this);
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _itemType = reader.ReadEnum<EditableItemType>();
        _syncItem = reader.ReadBoolean();
        if (_syncItem)
        {
            _slotIndex = reader.ReadInt32();
        }
        else
        {
            _editAsItem = reader.ReadBoolean();
            if (_editAsItem)
            {
                _inventoryId = reader.ReadInt32();
                _slotIndex = reader.ReadInt32();
            }
            else
            {
                _cellFace = reader.ReadCellFace();
            }
        }

        switch (_itemType)
        {
            case EditableItemType.TruthTable:
            case EditableItemType.MemoryBank:
                if (reader.ReadBoolean())
                {
                    _id = reader.ReadInt32();
                }

                var count = reader.ReadUInt16();
                _data = new byte[count];
                _data = reader.ReadBytes(count);
                break;

            default:
                _delay = reader.ReadInt32();
                break;
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_itemType);
        writer.Write(_syncItem);
        if (_syncItem)
        {
            writer.Write(_slotIndex);
        }
        else
        {
            writer.Write(_editAsItem);
            if (_editAsItem)
            {
                writer.Write(_inventoryId);
                writer.Write(_slotIndex);
            }
            else
            {
                writer.Write(_cellFace);
            }
        }

        switch (_itemType)
        {
            case EditableItemType.TruthTable:
            case EditableItemType.MemoryBank:
                writer.Write(_id.HasValue);
                if (_id.HasValue)
                {
                    writer.Write(_id.Value);
                }

                writer.Write((ushort)_data.Length);
                writer.Write(_data);
                break;
            default:
                writer.Write(_delay);
                break;
        }
    }

    private void ReplaceDataAtSlot(IInventory inventory, int slotIndex, Func<int, int> newData)
    {
        var value = inventory.GetSlotValue(slotIndex);
        var count = inventory.GetSlotCount(slotIndex);
        inventory.RemoveSlotItems(slotIndex, count);
        var newValue = Terrain.ReplaceData(value, newData(Terrain.ExtractData(value)));
        inventory.AddSlotItems(slotIndex, newValue, count);
    }
}
