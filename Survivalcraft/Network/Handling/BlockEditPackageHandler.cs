using EntitySystem.Core;

using Game.Network.Transport;

namespace Game.Network.Handling;

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

        NetworkSender.SendTo(client, new OpenInventoryPackage { InventoryId = inventory.Id });
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
        switch (package)
        {
            case OpenInventoryPackage message1:
            {
                if (isServer)
                {
                    var inventory = subsystemInventories.GetInventoryById(message1.InventoryId);
                    if (inventory != null && SubsystemInventories.CanClientAccess(inventory, context.Sender))
                    {
                        ReplyOpenInventory(inventory, context.Sender);
                    }
                }
                else
                {
                    var inventory = subsystemInventories.GetInventoryById(message1.InventoryId);
                    if (inventory != null)
                    {
                        var player = CommonLib.LocalCharacter;
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
                            player.ComponentMiner.ComponentCharacter?.ComponentGui.ModalPanelWidget =
                                new DispenserWidget(player.ComponentMiner.Inventory, componentDispenser);
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                        }
                        // 工具台
                        else if (inventory is ComponentCraftingTable componentCraftingTable)
                        {
                            player.ComponentMiner.ComponentCharacter?.ComponentGui.ModalPanelWidget =
                                new CraftingTableWidget(player.ComponentMiner.Inventory, componentCraftingTable);
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                        }
                    }
                }

                break;
            }
            case OpenBlockInventoryPackage message2:
            {
                if (isServer)
                {
                    if (!project.FindSubsystem<SubsystemNetworkInterest>(true)!
                            .IsPositionRelevant(context.Sender, new Vector2(message2.Point3.X, message2.Point3.Z)))
                    {
                        break;
                    }

                    var subsystemBlockEntities = project.FindSubsystem<SubsystemBlockEntities>(true)!;
                    var blockEntity =
                        subsystemBlockEntities.GetBlockEntity(message2.Point3.X, message2.Point3.Y, message2.Point3.Z);
                    var inventory = blockEntity?.Entity.FindComponent<IInventory>(false);
                    if (inventory != null)
                    {
                        ReplyOpenInventory(inventory, context.Sender);
                    }
                }

                break;
            }
            case CrossbowPullPackage message3:
            {
                if (isServer)
                {
                    var inventory = subsystemInventories.GetInventoryById(message3.InventoryId);

                    if (inventory != null &&
                        message3.SlotIndex >= 0 &&
                        message3.SlotIndex < inventory.SlotsCount &&
                        SubsystemInventories.CanClientAccess(inventory, context.Sender))
                    {
                        var theItemValue = inventory.GetSlotValue(message3.SlotIndex);
                        if (Terrain.ExtractContents(theItemValue) == 200)
                        {
                            var data = Terrain.ExtractData(theItemValue);
                            var value = Terrain.MakeBlockValue(200, 0, CrossbowBlock.SetDraw(data, 15));
                            inventory.RemoveSlotItems(message3.SlotIndex, 1);
                            inventory.AddSlotItems(message3.SlotIndex, value, 1);
                        }
                    }
                }

                break;
            }
            case OpenSignEditorPackage message4:
            {
                if (isServer)
                {
                    if (!project.FindSubsystem<SubsystemNetworkInterest>(true)!
                            .IsPositionRelevant(context.Sender, new Vector2(message4.Point3.X, message4.Point3.Z)))
                    {
                        break;
                    }

                    CommonLib.Net.QueuePackage(message4, PackageAudience.To(context.Sender));
                }
                else
                {
                    if (CommonLib.LocalCharacter != null)
                    {
                        project.FindSubsystem<SubsystemSignBlockBehavior>(true)!
                            .OpenEditor(CommonLib.LocalCharacter, message4.Point3);
                    }
                }

                break;
            }
        }
    }
}
