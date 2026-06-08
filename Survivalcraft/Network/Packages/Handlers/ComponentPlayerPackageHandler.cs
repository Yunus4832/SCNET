namespace Game.Network.Packages.Handlers;

public sealed class ComponentPlayerPackageHandler : PackageHandlerBase<ComponentPlayerPackage>
{
    public override void Handle(ComponentPlayerPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ComponentPlayerPackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        package.PlayerData = project.FindSubsystem<SubsystemPlayers>(true)!
            .FindPlayerData(playerData => playerData.ClientId == package.FromPlayerId);
        if (package is { NeedHandleMainPlayer: false, PlayerData.IsMainPlayer: true } &&
            package.Type != ComponentPlayerPackage.PlayerAction.AddExperience)
        {
            return;
        }

        if (package.From is not null && (package.PlayerData == null || package.PlayerData.ClientId != package.From.ID))
        {
            return;
        }

        switch (package.Type)
        {
            case ComponentPlayerPackage.PlayerAction.BodyUpdate:
                package.PlayerEvent(player =>
                {
                    ComponentBody body;
                    if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.ParentBodyChange))
                    {
                        if (player.ComponentBody.ParentBody != null)
                        {
                            body = player.ComponentBody.ParentBody;
                            if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.LookAnglesChange))
                            {
                                var loco = body.Locomotion;
                                loco?.NetLookAngles.SetNext(package.LookAngles);
                            }

                            if (package.PackageChangeFlag.HasFlag(
                                    ComponentPlayerPackage.ChangFlag.ChildLookAnglesChange))
                            {
                                player.ComponentBody.Locomotion?.NetLookAngles.SetNext(package.ChildLookAngles);
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
                        if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.LookAnglesChange))
                        {
                            player.ComponentLocomotion.NetLookAngles.SetNext(package.LookAngles);
                        }
                    }

                    if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.VelocityChange))
                    {
                        body.NetVelocity.SetNext(package.Velocity);
                    }

                    if (body.Locomotion != null)
                    {
                        if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.LadderChange))
                        {
                            body.Locomotion.LadderValue = package.LadderValue;
                        }
                        else
                        {
                            body.Locomotion.LadderValue = null;
                        }
                    }

                    if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.PositionChange))
                    {
                        body.NetPosition.SetNext(package.Position);
                    }

                    if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.RotationChange))
                    {
                        body.NetRotation.SetNext(package.Rotation);
                    }

                    if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.SneakChange))
                    {
                        body.IsSneaking = package.Sneaking;
                    }
                });
                break;
            case ComponentPlayerPackage.PlayerAction.InteractEvent:
                package.PlayerEvent(player =>
                {
                    player.AddInteractEvent(package.InteractEvent, package.NetInteractRay, package.NetInteractRaycast);
                    if (!isServer)
                    {
                        return;
                    }

                    package.Except = package.From;
                    netNode.QueuePackage(package);
                });
                break;
            case ComponentPlayerPackage.PlayerAction.AimEvent:
                package.PlayerEvent(player =>
                {
                    player.AddAimEvent(package.AimEvent, package.NetAimRay);
                    if (!isServer)
                    {
                        return;
                    }

                    package.Except = package.From;
                    netNode.QueuePackage(package);
                });
                break;
            case ComponentPlayerPackage.PlayerAction.DigEvent:
                package.PlayerEvent(player =>
                {
                    player.AddDigEvent(package.DigEvent, package.NetDigRay, package.NetDigRaycast);
                    if (!isServer)
                    {
                        return;
                    }

                    package.Except = package.From;
                    netNode.QueuePackage(package);
                });
                break;
            case ComponentPlayerPackage.PlayerAction.CreativeFlyChange:
                package.PlayerEvent(player =>
                {
                    player.ComponentLocomotion.IsCreativeFlyEnabled = package.IsCreativeFly;
                    if (!isServer)
                    {
                        return;
                    }

                    package.Except = package.From;
                    netNode.QueuePackage(package);
                });
                break;
            case ComponentPlayerPackage.PlayerAction.Hit:
                package.PlayerEvent(player =>
                {
                    project.FindEntityById(package.BodyId, entity =>
                    {
                        var body = entity.FindComponent<ComponentBody>();
                        if (body != null)
                        {
                            player.ComponentMiner.Hit(body, package.HitPosition, package.HitDirection);
                        }

                        if (!isServer)
                        {
                            return;
                        }

                        package.Except = package.From;
                        netNode.QueuePackage(package);
                    });
                });
                break;
            case ComponentPlayerPackage.PlayerAction.IntoPlaying:
                package.PlayerEvent(player => { player.ComponentHealth.IsInvulnerable = false; });
                break;
            case ComponentPlayerPackage.PlayerAction.Restart:
                package.PlayerEvent(player => { player.PlayerData.ReadyToRestart = true; });
                break;
            case ComponentPlayerPackage.PlayerAction.AddExperience:
                package.PlayerEvent(player =>
                {
                    player.ComponentLevel.NetAddExperience(package.Count, package.PlaySound);
                    player.PlayerData.Level = package.Level;
                });
                break;
            case ComponentPlayerPackage.PlayerAction.Drop:
                package.PlayerEvent(player => { player.DoDrop(); });
                break;
            case ComponentPlayerPackage.PlayerAction.DragDrop:
                package.PlayerEvent(player =>
                {
                    project.FindSubsystem<SubsystemInventories>(true)!.FindInventoryById(package.InventoryID,
                        inventory =>
                        {
                            // 丢弃背包内的物品，不是活动栏的
                            player.ViewWidget.NetDragDrop(package.HitPosition,
                                new InventoryDragData { Inventory = inventory, SlotIndex = package.ActiveSlot },
                                package.Count);
                        });
                });
                break;
            case ComponentPlayerPackage.PlayerAction.SyncStat:
                package.PlayerEvent(player =>
                {
                    if (package.Stat != null)
                    {
                        player.PlayerStats.Load(package.Stat);
                    }
                });
                break;
            case ComponentPlayerPackage.PlayerAction.PositionSet:
                package.PlayerEvent(player =>
                {
                    player.ComponentBody.Position = package.Position;
                    player.ComponentBody.Velocity = package.Velocity;
                });
                break;
        }
    }
}
