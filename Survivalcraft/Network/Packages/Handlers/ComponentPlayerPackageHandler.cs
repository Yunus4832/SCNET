using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentPlayerPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        PlayerData = project.FindSubsystem<SubsystemPlayers>(true)!
            .FindPlayerData(playerData => playerData.ClientId == FromPlayerId);
        if (!NeedHandleMainPlayer && PlayerData is { IsMainPlayer: true } && Type != PlayerAction.AddExperience)
        {
            return;
        }

        if (From != null && (PlayerData == null || PlayerData.ClientId != From.ID))
        {
            return;
        }

        switch (Type)
        {
            case PlayerAction.BodyUpdate:
                PlayerEvent(player =>
                {
                    ComponentBody body;
                    if (PackageChangeFlag.HasFlag(ChangFlag.ParentBodyChange))
                    {
                        if (player.ComponentBody.ParentBody != null)
                        {
                            body = player.ComponentBody.ParentBody;
                            if (PackageChangeFlag.HasFlag(ChangFlag.LookAnglesChange))
                            {
                                var loco = body.Locomotion;
                                loco?.NetLookAngles.SetNext(LookAngles);
                            }

                            if (PackageChangeFlag.HasFlag(ChangFlag.ChildLookAnglesChange))
                            {
                                player.ComponentBody.Locomotion?.NetLookAngles.SetNext(ChildLookAngles);
                            }
                        }
                        else
                        {
                            body = player.ComponentBody;
                        }
                    }
                    else
                    {
                        body = player.ComponentBody;
                        if (PackageChangeFlag.HasFlag(ChangFlag.LookAnglesChange))
                        {
                            player.ComponentLocomotion.NetLookAngles.SetNext(LookAngles);
                        }
                    }

                    if (PackageChangeFlag.HasFlag(ChangFlag.VelocityChange))
                    {
                        body.NetVelocity.SetNext(Velocity);
                    }

                    if (body.Locomotion != null)
                    {
                        if (PackageChangeFlag.HasFlag(ChangFlag.LadderChange))
                        {
                            body.Locomotion.LadderValue = LadderValue;
                        }
                        else
                        {
                            body.Locomotion.LadderValue = null;
                        }
                    }

                    if (PackageChangeFlag.HasFlag(ChangFlag.PositionChange))
                    {
                        body.NetPosition.SetNext(Position);
                    }

                    if (PackageChangeFlag.HasFlag(ChangFlag.RotationChange))
                    {
                        body.NetRotation.SetNext(Rotation);
                    }

                    if (PackageChangeFlag.HasFlag(ChangFlag.SneakChange))
                    {
                        body.IsSneaking = Sneaking;
                    }
                });
                break;
            case PlayerAction.InteractEvent:
                PlayerEvent(player =>
                {
                    player.AddInteractEvent(InteractEvent, NetInteractRay, NetInteractRaycast);
                    if (isServer)
                    {
                        Except = From;
                        netNode.QueuePackage(this);
                    }
                });
                break;
            case PlayerAction.AimEvent:
                PlayerEvent(player =>
                {
                    player.AddAimEvent(AimEvent, NetAimRay);
                    if (isServer)
                    {
                        Except = From;
                        netNode.QueuePackage(this);
                    }
                });
                break;
            case PlayerAction.DigEvent:
                PlayerEvent(player =>
                {
                    player.AddDigEvent(DigEvent, NetDigRay, NetDigRaycast);
                    if (isServer)
                    {
                        Except = From;
                        netNode.QueuePackage(this);
                    }
                });
                break;
            case PlayerAction.CreativeFlyChange:
                PlayerEvent(player =>
                {
                    player.ComponentLocomotion.IsCreativeFlyEnabled = IsCreativeFly;
                    if (isServer)
                    {
                        Except = From;
                        netNode.QueuePackage(this);
                    }
                });
                break;
            case PlayerAction.Hit:
                PlayerEvent(player =>
                {
                    project.FindEntityById(BodyId, entity =>
                    {
                        var body = entity.FindComponent<ComponentBody>();
                        if (body != null)
                        {
                            player.ComponentMiner.Hit(body, HitPosition, HitDirection);
                        }

                        if (!isServer)
                        {
                            return;
                        }

                        Except = From;
                        netNode.QueuePackage(this);
                    });
                });
                break;
            case PlayerAction.IntoPlaying:
                PlayerEvent(player => { player.ComponentHealth.IsInvulnerable = false; });
                break;
            case PlayerAction.Restart:
                PlayerEvent(player => { player.PlayerData.ReadyToRestart = true; });
                break;
            case PlayerAction.AddExperience:
                PlayerEvent(player =>
                {
                    player.ComponentLevel.NetAddExperience(Count, PlaySound);
                    player.PlayerData.Level = Level;
                });
                break;
            case PlayerAction.Drop:
                PlayerEvent(player => { player.DoDrop(); });
                break;
            case PlayerAction.DragDrop:
                // 我去，别这样搞啊，回调地狱可是会很头疼的！！！！！
                PlayerEvent(player =>
                {
                    project.FindSubsystem<SubsystemInventories>(true)!.FindInventoryById(InventoryID, inventory =>
                    {
                        // 丢弃背包内的物品，不是活动栏的
                        player.ViewWidget.NetDragDrop(HitPosition,
                            new InventoryDragData { Inventory = inventory, SlotIndex = ActiveSlot }, Count);
                    });
                });
                break;
            case PlayerAction.SyncStat:
                PlayerEvent(player =>
                {
                    if (Stat != null)
                    {
                        player.PlayerStats.Load(Stat);
                    }
                });
                break;
            case PlayerAction.PositionSet:
                PlayerEvent(player =>
                {
                    player.ComponentBody.Position = Position;
                    player.ComponentBody.Velocity = Velocity;
                });
                break;
        }
    }
}

public sealed class ComponentPlayerPackageHandler : PackageHandlerBase<ComponentPlayerPackage>
{
    public override void Handle(ComponentPlayerPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ComponentPlayerPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
