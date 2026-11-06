using EntitySystem.Core;

namespace Game.Network.Handling;

public class BlockEditPackageHandler : PackageHandlerBase<BlockEditPackage>
{
    private static void ReplyOpenInventory(IInventory inventory, WorldConnection client)
    {
        if (!SubsystemInventories.CanClientAccess(inventory, client)) { return; }

        if (inventory is ComponentDispenser &&
            inventory.Project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode == GameMode.Adventure)
        {
            return;
        }

        if (inventory is Component component &&
            component.Entity.FindComponent<ComponentBlockEntity>() is { } block)
        {
            component.Project.FindSubsystem<SubsystemNetworkInterest>(true)!
                .EnsureBlockEntityObserved(client, block);
        }

        NetworkSender.SendTo(client, new OpenInventoryPackage { InventoryId = inventory.Id });
    }

    protected static bool IsAuthorityReply(PackageReceiveContext context) =>
        !context.IsServer &&
        !context.Runtime.IsClosed &&
        context.Node?.Server is { } authority &&
        ReferenceEquals(context.Sender, authority) &&
        ReferenceEquals(authority.World, context.Runtime);

    public override void Handle(BlockEditPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (context.Sender == null)
        {
            Log.Information("出现空玩家打开背包");
            return;
        }

        if (context.Project is null)
        {
            return;
        }

        var project = context.Project;
        switch (package)
        {
            case OpenInventoryPackage message1:
            {
                var subsystemInventories = project.FindSubsystem<SubsystemInventories>(true)!;
                if (isServer)
                {
                    var inventory = subsystemInventories.GetInventoryById(message1.InventoryId);
                    if (inventory != null)
                    {
                        ReplyOpenInventory(inventory, context.Sender);
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
                    var inventory =
                        project.FindSubsystem<SubsystemInventories>(true)!.GetInventoryById(message3.InventoryId);

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
                    if (!project.FindSubsystem<SubsystemSignBlockBehavior>(true)!
                            .CanEditSign(message4.Point3, context.Sender))
                    {
                        break;
                    }

                    NetworkSender.Send(project, message4, PackageAudience.To(context.Sender));
                }


                break;
            }
        }
    }
}
