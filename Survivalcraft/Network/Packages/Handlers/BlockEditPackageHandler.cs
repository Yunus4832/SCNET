using EntitySystem.Core;

namespace Game.Network.Packages.Handlers;

public sealed class BlockEditPackageHandler : PackageHandlerBase<BlockEditPackage>
{
    private static void ReplyOpenInventory(IInventory inventory, Client client)
    {
        if (inventory is Component component &&
            component.Entity.FindComponent<ComponentBlockEntity>() is { } block)
        {
            component.Project.FindSubsystem<SubsystemNetworkInterest>(true)!
                .EnsureBlockEntityObserved(client, block);
        }

        NetworkSender.SendTo(client, new BlockEditPackage(inventory));
    }

    public override void Handle(BlockEditPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (context.Sender == null)
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
                    if (inventory != null && SubsystemInventories.CanClientAccess(inventory, context.Sender))
                    {
                        ReplyOpenInventory(inventory, context.Sender);
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
                    if (!project.FindSubsystem<SubsystemNetworkInterest>(true)!
                            .IsPositionRelevant(context.Sender, new Vector2(package.Point3.X, package.Point3.Z)))
                    {
                        break;
                    }

                    var subsystemBlockEntities = project.FindSubsystem<SubsystemBlockEntities>(true)!;
                    var blockEntity =
                        subsystemBlockEntities.GetBlockEntity(package.Point3.X, package.Point3.Y, package.Point3.Z);
                    var inventory = blockEntity?.Entity.FindComponent<IInventory>(false);
                    if (inventory != null)
                    {
                        ReplyOpenInventory(inventory, context.Sender);
                    }
                }

                break;
            case BlockEditPackage.EventType.CrossbowPull:
                if (isServer)
                {
                    var inventory = subsystemInventories.GetInventoryById(package.InventoryId);

                    if (inventory != null && package.SlotIndex >= 0 && package.SlotIndex < inventory.SlotsCount &&
                        SubsystemInventories.CanClientAccess(inventory, context.Sender))
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
                    if (!project.FindSubsystem<SubsystemNetworkInterest>(true)!
                            .IsPositionRelevant(context.Sender, new Vector2(package.Point3.X, package.Point3.Z)))
                    {
                        break;
                    }

                    CommonLib.Net.QueuePackage(package, PackageAudience.To(context.Sender));
                }
                else
                {
                    if (CommonLib.MainPlayer != null)
                    {
                        project.FindSubsystem<SubsystemSignBlockBehavior>(true)!
                            .OpenEditor(CommonLib.MainPlayer, package.Point3);
                    }
                }

                break;
        }
    }
}
