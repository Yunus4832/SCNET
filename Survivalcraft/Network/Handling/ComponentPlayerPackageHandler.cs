using Game.Network.Transport;

namespace Game.Network.Handling;

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
                ComponentPlayerPackage.PlayerAction.Hit
                or ComponentPlayerPackage.PlayerAction.CreativeFlyChange => true,
            _ => false
        };
    }

    internal static bool AcceptsSender(bool isServer, Client? sender, PlayerData? player)
    {
        return !isServer || sender is not null && player is not null && ReferenceEquals(player.Client, sender);
    }

    internal static bool AreDropParametersValid(int slot, int slotsCount, int count, int available, Vector3 velocity)
    {
        return slot >= 0 &&
               slot < slotsCount &&
               count > 0 &&
               count <= available &&
               float.IsFinite(velocity.X) &&
               float.IsFinite(velocity.Y) &&
               float.IsFinite(velocity.Z) &&
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
        var playerData = project.FindSubsystem<SubsystemPlayers>(true)!
            .FindPlayerData(playerData => playerData.ClientId == package.FromPlayerId);
        if (!AcceptsSender(isServer, context.Sender, playerData))
        {
            return;
        }

        void PlayerEvent(Action<ComponentPlayer> action)
        {
            if (playerData?.ComponentPlayer is { } player) { action(player); }
        }

        switch (package)
        {
            case PlayerStatePackage message:
                PlayerEvent(player =>
                    {
                        var body = ResolveMovementBody(
                            player.ComponentBody,
                            message.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.ParentBodyChange)
                        );
                        if (body is null)
                        {
                            return;
                        }

                        if (message.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.ParentBodyChange))
                        {
                            if (message.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.LookAnglesChange))
                            {
                                body.Locomotion?.NetLookAngles.SetNext(message.LookAngles);
                            }

                            if (message.PackageChangeFlag.HasFlag(
                                    ComponentPlayerPackage.ChangFlag.ChildLookAnglesChange
                                ))
                            {
                                player.ComponentBody.Locomotion?.NetLookAngles.SetNext(message.ChildLookAngles);
                            }
                        }
                        else
                        {
                            if (message.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.LookAnglesChange))
                            {
                                player.ComponentLocomotion.NetLookAngles.SetNext(message.LookAngles);
                            }
                        }

                        if (message.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.VelocityChange))
                        {
                            body.NetVelocity.SetNext(message.Velocity);
                        }

                        if (body.Locomotion != null)
                        {
                            if (message.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.LadderChange))
                            {
                                body.Locomotion.LadderValue = message.LadderValue;
                            }
                            else
                            {
                                body.Locomotion.LadderValue = null;
                            }
                        }

                        if (message.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.PositionChange))
                        {
                            body.NetPosition.SetNext(message.Position);
                        }

                        if (message.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.RotationChange))
                        {
                            body.NetRotation.SetNext(message.Rotation);
                        }

                        if (message.PackageChangeFlag.HasFlag(ComponentPlayerPackage.ChangFlag.SneakChange))
                        {
                            body.IsSneaking = message.Sneaking;
                        }
                    }
                );
                break;
            case PlayerInteractionPackage message:
                PlayerEvent(player =>
                    {
                        player.AddInteractEvent(
                            message.InteractEvent,
                            message.NetInteractRay,
                            PlayerPackageBuilder.RestoreHit(message.NetInteractRaycast),
                            message.PlacementRequestId,
                            message.PlacementCell,
                            message.PlacementExpectedValue
                        );
                        if (!isServer)
                        {
                            return;
                        }

                        NetworkSender.SendToObservers(player.Entity, message, context.Sender);
                    }
                );
                break;
            case PlayerAimPackage message:
                PlayerEvent(player =>
                    {
                        player.AddAimEvent(message.AimEvent, message.NetAimRay);
                        if (!isServer)
                        {
                            return;
                        }

                        NetworkSender.SendToObservers(player.Entity, message, context.Sender);
                    }
                );
                break;
            case PlayerDigPackage message:
                PlayerEvent(player =>
                    {
                        player.AddDigEvent(
                            message.DigEvent,
                            message.NetDigRay,
                            PlayerPackageBuilder.RestoreHit(message.NetDigRaycast),
                            message.DigRequestId,
                            message.DigExpectedValue
                        );
                        if (!isServer)
                        {
                            return;
                        }

                        NetworkSender.SendToObservers(player.Entity, message, context.Sender);
                    }
                );
                break;
            case PlayerFlightPackage message:
                PlayerEvent(player =>
                    {
                        player.ComponentLocomotion.IsCreativeFlyEnabled = message.IsCreativeFly;
                        if (!isServer)
                        {
                            return;
                        }

                        NetworkSender.SendToObservers(player.Entity, message, context.Sender);
                    }
                );
                break;
            case PlayerHitPackage message:
                PlayerEvent(player =>
                    {
                        project.FindEntityById(
                            message.BodyId,
                            entity =>
                            {
                                var body = entity.FindComponent<ComponentBody>();
                                if (body != null)
                                {
                                    player.ComponentMiner.Hit(body, message.HitPosition, message.HitDirection);
                                }

                                if (!isServer)
                                {
                                    return;
                                }

                                NetworkSender.SendToObservers(player.Entity, message, context.Sender);
                            }
                        );
                    }
                );
                break;
            case PlayerReadyPackage message:
                PlayerEvent(player => { player.ComponentHealth.IsInvulnerable = false; });
                break;
            case PlayerRestartPackage message:
                PlayerEvent(player => { player.PlayerData.ReadyToRestart = true; });
                break;
            case PlayerExperiencePackage message:
                PlayerEvent(player =>
                    {
                        player.ComponentLevel.NetAddExperience(message.Count, message.PlaySound);
                        player.PlayerData.Level = message.Level;
                    }
                );
                break;
            case PlayerDropPackage message:
                PlayerEvent(player => { player.DoDrop(); });
                break;
            case PlayerDragDropPackage message:
                PlayerEvent(player =>
                    {
                        project.FindSubsystem<SubsystemInventories>(true)!.FindInventoryById(
                            message.InventoryID,
                            inventory =>
                            {
                                if (!SubsystemInventories.CanClientAccess(inventory, context.Sender!) ||
                                    message.ActiveSlot < 0 ||
                                    message.ActiveSlot >= inventory.SlotsCount ||
                                    !AreDropParametersValid(
                                        message.ActiveSlot,
                                        inventory.SlotsCount,
                                        message.Count,
                                        inventory.GetSlotCount(message.ActiveSlot),
                                        message.HitPosition
                                    ))
                                {
                                    return;
                                }

                                var value = inventory.GetSlotValue(message.ActiveSlot);
                                var removed = inventory.RemoveSlotItems(message.ActiveSlot, message.Count);
                                if (removed > 0)
                                {
                                    project.FindSubsystem<SubsystemPickables>(true)!.AddPickable(
                                        value,
                                        removed,
                                        player.ComponentCreatureModel.EyePosition,
                                        message.HitPosition,
                                        null
                                    );
                                }
                            }
                        );
                    }
                );
                break;
            case PlayerStatisticsPackage message:
                PlayerEvent(player =>
                    {
                        var statistics = new EntitySystem.TemplatesDatabase.ValuesDictionary();
                        statistics.ApplyOverridesUseMessagePack(message.Stat);
                        player.PlayerStats.Load(statistics);
                    }
                );
                break;
            case PlayerPositionPackage message:
                PlayerEvent(player =>
                    {
                        player.ComponentBody.ApplyAuthoritativePosition(message.Position, message.Velocity);
                    }
                );
                break;
        }
    }
}
