using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class BlockEditPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (From == null)
        {
            Log.Information("出现空玩家打开背包");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;

        var subsystemInventories = project.FindSubsystem<SubsystemInventories>(true)!;
        switch (Type)
        {
            case EventType.OpenInventoryByID:
                if (isServer)
                {
                    var inventory = subsystemInventories.GetInventoryById(InventoryId);
                    if (inventory != null)
                    {
                        IPackage package = new BlockEditPackage(inventory);
                        package.To = From;
                        CommonLib.Net.QueuePackage(package);
                    }
                }
                else
                {
                    var inventory = subsystemInventories.GetInventoryById(InventoryId);
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
                    var subsystemBlockEntities = project.FindSubsystem<SubsystemBlockEntities>(true)!;
                    var blockEntity = subsystemBlockEntities.GetBlockEntity(Point3.X, Point3.Y, Point3.Z);
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
                    var inventory = subsystemInventories.GetInventoryById(InventoryId);

                    if (inventory != null)
                    {
                        var theItemValue = inventory.GetSlotValue(SlotIndex);
                        if (Terrain.ExtractContents(theItemValue) == 200)
                        {
                            var data = Terrain.ExtractData(theItemValue);
                            var value = Terrain.MakeBlockValue(200, 0, CrossbowBlock.SetDraw(data, 15));
                            inventory.RemoveSlotItems(SlotIndex, 1);
                            inventory.AddSlotItems(SlotIndex, value, 1);
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
                            new EditSignDialog(project.FindSubsystem<SubsystemSignBlockBehavior>(true)!, Point3));
                    }
                }

                break;
        }
    }
}

public sealed class BlockEditPackageHandler : PackageHandlerBase<BlockEditPackage>
{
    public override void Handle(BlockEditPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(BlockEditPackage)}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
