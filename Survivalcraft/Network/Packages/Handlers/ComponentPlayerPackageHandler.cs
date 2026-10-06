namespace Game.Network.Packages.Handlers;

public sealed class ComponentPlayerPackageHandler : PackageHandlerBase<ComponentPlayerPackage>
{
    internal static ComponentBody? ResolveMovementBody(ComponentBody playerBody, bool mounted)
    {
        if (mounted != (playerBody.ParentBody is not null))
        {
            return null;
        }

        return playerBody.ParentBody ?? playerBody;
    }

    internal static bool AcceptsDirection(ComponentPlayerPackage.PlayerAction action, bool isServer)
    {
        return action switch
        {
            ComponentPlayerPackage.PlayerAction.AddExperience or ComponentPlayerPackage.PlayerAction.SyncStat or
                ComponentPlayerPackage.PlayerAction.PositionSet => !isServer,
            ComponentPlayerPackage.PlayerAction.IntoPlaying or ComponentPlayerPackage.PlayerAction.Restart or
                ComponentPlayerPackage.PlayerAction.Drop or ComponentPlayerPackage.PlayerAction.DragDrop => isServer,
            ComponentPlayerPackage.PlayerAction.BodyUpdate or ComponentPlayerPackage.PlayerAction.InteractEvent or
                ComponentPlayerPackage.PlayerAction.AimEvent or ComponentPlayerPackage.PlayerAction.DigEvent or
                ComponentPlayerPackage.PlayerAction.Hit or ComponentPlayerPackage.PlayerAction.CreativeFlyChange => true,
            _ => false
        };
    }

    internal static bool AcceptsSender(bool isServer, Client? sender, PlayerData? player)
    {
        return !isServer || sender is not null && player is not null && ReferenceEquals(player.Client, sender);
    }

    internal static bool AreDropParametersValid(int slot, int slotsCount, int count, int available, Vector3 velocity)
    {
        return slot >= 0 && slot < slotsCount && count > 0 && count <= available &&
               float.IsFinite(velocity.X) && float.IsFinite(velocity.Y) && float.IsFinite(velocity.Z) &&
               velocity.LengthSquared() <= 144.01f;
    }

    public override void Handle(ComponentPlayerPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ComponentPlayerPackage)}");
            return;
        }

        if (GameManager.Project is null || !AcceptsDirection(package.Type, isServer))
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

        if (!AcceptsSender(isServer, context.Sender, package.PlayerData))
        {
            return;
        }

        switch (package.Type)
        {
            case ComponentPlayerPackage.PlayerAction.BodyUpdate:
                package.PlayerEvent(player =>
                {
                    var body = ResolveMovementBody(player.ComponentBody,
                        package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.ParentBodyChange));
                    if (body is null)
                    {
                        return;
                    }

                    if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.ParentBodyChange))
                    {
                        if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.LookAnglesChange))
                        {
                            body.Locomotion?.NetLookAngles.SetNext(package.LookAngles);
                        }

                        if (package.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.ChildLookAnglesChange))
                        {
                            player.ComponentBody.Locomotion?.NetLookAngles.SetNext(package.ChildLookAngles);
                        }
                    }
                    else
                    {
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

                    NetworkSender.SendToObservers(player.Entity, package, context.Sender);
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

                    NetworkSender.SendToObservers(player.Entity, package, context.Sender);
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

                    NetworkSender.SendToObservers(player.Entity, package, context.Sender);
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

                    NetworkSender.SendToObservers(player.Entity, package, context.Sender);
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

                        NetworkSender.SendToObservers(player.Entity, package, context.Sender);
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
                            if (!SubsystemInventories.CanClientAccess(inventory, context.Sender!) ||
                                package.ActiveSlot < 0 || package.ActiveSlot >= inventory.SlotsCount ||
                                !AreDropParametersValid(package.ActiveSlot, inventory.SlotsCount, package.Count,
                                    inventory.GetSlotCount(package.ActiveSlot), package.HitPosition))
                            {
                                return;
                            }

                            var value = inventory.GetSlotValue(package.ActiveSlot);
                            var removed = inventory.RemoveSlotItems(package.ActiveSlot, package.Count);
                            if (removed > 0)
                            {
                                project.FindSubsystem<SubsystemPickables>(true)!.AddPickable(value, removed,
                                    player.ComponentCreatureModel.EyePosition, package.HitPosition, null);
                            }
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
