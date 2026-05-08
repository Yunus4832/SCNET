namespace Game.NetWork.Packages;

public class BlockEditPackage : IPackage
{
    public enum EventType
    {
        OpenInventoryByID, // 打开背包，通过背包id， 注：背包指的是IInventory
        OpenInventoryByPoint, // 打开背包，通过位置，通常是BlockEntity，例如箱子，炉子
        CrossbowPull, // 十字弩拉弓
        EditSign // 编辑牌子
    }

    private int _inventoryId;

    private Point3 _point3;

    private int _slotIndex;

    private EventType _type;

    public byte ID => (byte)PackageType.BlockEdit;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public BlockEditPackage()
    {
    }

    public BlockEditPackage(IInventory inventory)
    {
        _inventoryId = inventory.Id;
        _type = EventType.OpenInventoryByID;
    }

    public BlockEditPackage(Point3 point3, EventType type)
    {
        _point3 = point3;
        _type = type;
    }

    public BlockEditPackage(IInventory inventory, int slotIndex, EventType type)
    {
        _inventoryId = inventory.Id;
        _slotIndex = slotIndex;
        _type = type;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_type);
        switch (_type)
        {
            case EventType.OpenInventoryByID:
                writer.Write(_inventoryId);
                break;
            case EventType.OpenInventoryByPoint:
                writer.Write(_point3);
                break;
            case EventType.CrossbowPull:
                writer.Write(_inventoryId);
                writer.Write(_slotIndex);
                break;
            case EventType.EditSign:
                writer.Write(_point3);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _type = reader.ReadEnum<EventType>();
        switch (_type)
        {
            case EventType.OpenInventoryByID:
                _inventoryId = reader.ReadInt32();
                break;
            case EventType.OpenInventoryByPoint:
                _point3 = reader.ReadPoint3();
                break;
            case EventType.CrossbowPull:
                _inventoryId = reader.ReadInt32();
                _slotIndex = reader.ReadInt32();
                break;
            case EventType.EditSign:
                _point3 = reader.ReadPoint3();
                break;
        }
    }

    public void Handle(ProjectNet? projectNet, NetNode netNode, bool isServer)
    {
        if (From == null) // 会出现空么？？？？？
        {
            Log.Information("出现空玩家打开背包");
            return;
        }

        if (projectNet == null)
        {
            return;
        }

        var subsystemInventories = projectNet.FindSubsystem<SubsystemInventories>(true)!;
        switch (_type)
        {
            case EventType.OpenInventoryByID:
                if (isServer)
                {
                    var inventory = subsystemInventories.GetInventoryById(_inventoryId);
                    if (inventory != null)
                    {
                        IPackage package = new BlockEditPackage(inventory);
                        package.To = From;
                        CommonLib.Net.QueuePackage(package);
                    }
                }
                else
                {
                    var inventory = subsystemInventories.GetInventoryById(_inventoryId);
                    if (inventory != null)
                    {
                        var player = CommonLib.MainPlayer;
                        if (player is null)
                        {
                            return;
                        }

                        // 箱子
                        if (inventory is ComponentChest componentChest)
                        {
                            player.ComponentGui.ModalPanelWidget =
                                new ChestWidget(player.ComponentMiner.Inventory, componentChest);
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                        }
                        // 熔炉
                        else if (inventory is ComponentFurnace componentFurnace)
                        {
                            player.ComponentGui.ModalPanelWidget =
                                new FurnaceWidget(player.ComponentMiner.Inventory, componentFurnace);
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                        }
                        // 发射器
                        else if (inventory is ComponentDispenser componentDispenser)
                        {
                            player.ComponentMiner.ComponentPlayer?.ComponentGui.ModalPanelWidget =
                                new DispenserWidget(player.ComponentMiner.Inventory, componentDispenser);
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                        }
                        // 工具台
                        else if (inventory is ComponentCraftingTable componentCraftingTable)
                        {
                            player.ComponentMiner.ComponentPlayer?.ComponentGui.ModalPanelWidget =
                                new CraftingTableWidget(player.ComponentMiner.Inventory, componentCraftingTable);
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                        }
                    }
                }

                break;
            case EventType.OpenInventoryByPoint:
                if (isServer)
                {
                    var subsystemBlockEntities = projectNet.FindSubsystem<SubsystemBlockEntities>(true)!;
                    var blockEntity = subsystemBlockEntities.GetBlockEntity(_point3.X, _point3.Y, _point3.Z);
                    var inventory = blockEntity?.Entity.FindComponent<IInventory>(false);
                    if (inventory != null)
                    {
                        IPackage package = new BlockEditPackage(inventory);
                        package.To = From;
                        CommonLib.Net.QueuePackage(package);
                    }
                }

                break;
            case EventType.CrossbowPull:
                if (isServer)
                {
                    var inventory = subsystemInventories.GetInventoryById(_inventoryId);

                    if (inventory != null)
                    {
                        var theItemValue = inventory.GetSlotValue(_slotIndex);
                        if (Terrain.ExtractContents(theItemValue) == 200)
                        {
                            var data = Terrain.ExtractData(theItemValue);
                            var value = Terrain.MakeBlockValue(200, 0, CrossbowBlock.SetDraw(data, 15));
                            inventory.RemoveSlotItems(_slotIndex, 1);
                            inventory.AddSlotItems(_slotIndex, value, 1);
                        }
                    }
                }

                break;
            case EventType.EditSign:
                if (isServer)
                {
                    To = From;
                    CommonLib.Net.QueuePackage(this);
                }
                else
                {
                    if (CommonLib.MainPlayer != null)
                    {
                        DialogsManager.ShowDialog(CommonLib.MainPlayer.GuiWidget,
                            new EditSignDialog(projectNet.FindSubsystem<SubsystemSignBlockBehavior>(true)!, _point3));
                    }
                }

                break;
        }
    }
}
