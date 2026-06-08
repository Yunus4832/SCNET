namespace Game.Network.Packages.Handlers;

public sealed class BlockEditPackageHandler : PackageHandlerBase<BlockEditPackage>
{
    public override void Handle(BlockEditPackage package, NetNode? netNode, bool isServer)
    {
        if (package.From == null)
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
        switch (package.Type)
        {
            case BlockEditPackage.EventType.OpenInventoryByID:
                if (isServer)
                {
                    var inventory = subsystemInventories.GetInventoryById(package.InventoryId);
                    if (inventory != null)
                    {
                        IPackage newPackage = new BlockEditPackage(inventory);
                        newPackage.To = package.From;
                        CommonLib.Net.QueuePackage(newPackage);
                    }
                }
                else
                {
                    var inventory = subsystemInventories.GetInventoryById(package.InventoryId);
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
            case BlockEditPackage.EventType.OpenInventoryByPoint:
                if (isServer)
                {
                    var subsystemBlockEntities = project.FindSubsystem<SubsystemBlockEntities>(true)!;
                    var blockEntity = subsystemBlockEntities.GetBlockEntity(package.Point3.X, package.Point3.Y, package.Point3.Z);
                    var inventory = blockEntity?.Entity.FindComponent<IInventory>(false);
                    if (inventory != null)
                    {
                        IPackage newPackage = new BlockEditPackage(inventory);
                        newPackage.To = package.From;
                        CommonLib.Net.QueuePackage(newPackage);
                    }
                }

                break;
            case BlockEditPackage.EventType.CrossbowPull:
                if (isServer)
                {
                    var inventory = subsystemInventories.GetInventoryById(package.InventoryId);

                    if (inventory != null)
                    {
                        var theItemValue = inventory.GetSlotValue(package.SlotIndex);
                        if (Terrain.ExtractContents(theItemValue) == 200)
                        {
                            var data = Terrain.ExtractData(theItemValue);
                            var value = Terrain.MakeBlockValue(200, 0, CrossbowBlock.SetDraw(data, 15));
                            inventory.RemoveSlotItems(package.SlotIndex, 1);
                            inventory.AddSlotItems(package.SlotIndex, value, 1);
                        }
                    }
                }

                break;
            case BlockEditPackage.EventType.EditSign:
                if (isServer)
                {
                    package.To = package.From;
                    CommonLib.Net.QueuePackage(package);
                }
                else
                {
                    if (CommonLib.MainPlayer != null)
                    {
                        DialogsManager.ShowDialog(CommonLib.MainPlayer.GuiWidget,
                            new EditSignDialog(project.FindSubsystem<SubsystemSignBlockBehavior>(true)!, package.Point3));
                    }
                }

                break;
        }
    }
}
