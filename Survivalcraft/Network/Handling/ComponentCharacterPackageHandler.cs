using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ComponentCharacterPackageHandler : PackageHandlerBase<ComponentCharacterPackage>
{
    internal static ComponentBody? ResolveMovementBody(ComponentBody playerBody, bool mounted)
    {
        if (mounted != (playerBody.ParentBody is not null))
        {
            return null;
        }

        return playerBody.ParentBody ?? playerBody;
    }

    internal static bool AcceptsDirection(ComponentCharacterPackage.CharacterAction action, bool isServer)
    {
        return action switch
        {
            ComponentCharacterPackage.CharacterAction.AddExperience
                or ComponentCharacterPackage.CharacterAction.SyncStat or
                ComponentCharacterPackage.CharacterAction.PositionSet => !isServer,
            ComponentCharacterPackage.CharacterAction.IntoPlaying or ComponentCharacterPackage.CharacterAction.Restart
                or
                ComponentCharacterPackage.CharacterAction.Drop
                or ComponentCharacterPackage.CharacterAction.DragDrop => isServer,
            ComponentCharacterPackage.CharacterAction.BodyUpdate
                or ComponentCharacterPackage.CharacterAction.InteractEvent or
                ComponentCharacterPackage.CharacterAction.AimEvent or ComponentCharacterPackage.CharacterAction.DigEvent
                or
                ComponentCharacterPackage.CharacterAction.Hit
                or ComponentCharacterPackage.CharacterAction.CreativeFlyChange => true,
            _ => false
        };
    }

    internal static bool AcceptsSender(bool isServer, Client? sender, CharacterData? player)
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

    public override void Handle(ComponentCharacterPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ComponentCharacterPackage)}");
            return;
        }

        if (GameManager.Project is null || !AcceptsDirection(package.Type, isServer))
        {
            return;
        }

        var project = GameManager.Project;
        var characterData = project.FindSubsystem<SubsystemCharacters>(true)!
            .FindCharacterData(characterData => characterData.ClientId == package.ControllerSessionId);
        if (!AcceptsSender(isServer, context.Sender, characterData))
        {
            return;
        }

        void PlayerEvent(Action<ComponentCharacter> action)
        {
            if (characterData?.ComponentCharacter is { } player) { action(player); }
        }

        switch (package)
        {
            case CharacterStatePackage message:
                PlayerEvent(player =>
                    {
                        var body = ResolveMovementBody(
                            player.ComponentBody,
                            message.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.ParentBodyChange)
                        );
                        if (body is null)
                        {
                            return;
                        }

                        if (message.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.ParentBodyChange))
                        {
                            if (message.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.LookAnglesChange))
                            {
                                body.Locomotion?.NetLookAngles.SetNext(message.LookAngles);
                            }

                            if (message.PackageChangeFlag.HasFlag(
                                    ComponentCharacterPackage.ChangFlag.ChildLookAnglesChange
                                ))
                            {
                                player.ComponentBody.Locomotion?.NetLookAngles.SetNext(message.ChildLookAngles);
                            }
                        }
                        else
                        {
                            if (message.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.LookAnglesChange))
                            {
                                player.ComponentLocomotion.NetLookAngles.SetNext(message.LookAngles);
                            }
                        }

                        if (message.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.VelocityChange))
                        {
                            body.NetVelocity.SetNext(message.Velocity);
                        }

                        if (body.Locomotion != null)
                        {
                            if (message.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.LadderChange))
                            {
                                body.Locomotion.LadderValue = message.LadderValue;
                            }
                            else
                            {
                                body.Locomotion.LadderValue = null;
                            }
                        }

                        if (message.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.PositionChange))
                        {
                            body.NetPosition.SetNext(message.Position);
                        }

                        if (message.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.RotationChange))
                        {
                            body.NetRotation.SetNext(message.Rotation);
                        }

                        if (message.PackageChangeFlag.HasFlag(ComponentCharacterPackage.ChangFlag.SneakChange))
                        {
                            body.IsSneaking = message.Sneaking;
                        }
                    }
                );
                break;
            case CharacterInteractionPackage message:
                PlayerEvent(player =>
                    {
                        player.AddInteractEvent(
                            message.InteractEvent,
                            message.NetInteractRay,
                            CharacterPackageBuilder.RestoreHit(message.NetInteractRaycast),
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
            case CharacterAimPackage message:
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
            case CharacterDigPackage message:
                PlayerEvent(player =>
                    {
                        player.AddDigEvent(
                            message.DigEvent,
                            message.NetDigRay,
                            CharacterPackageBuilder.RestoreHit(message.NetDigRaycast),
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
            case CharacterFlightPackage message:
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
            case CharacterHitPackage message:
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
            case CharacterReadyPackage message:
                PlayerEvent(player => { player.ComponentHealth.IsInvulnerable = false; });
                break;
            case CharacterRestartPackage message:
                PlayerEvent(player => { player.CharacterData.ReadyToRestart = true; });
                break;
            case CharacterExperiencePackage message:
                PlayerEvent(player =>
                    {
                        player.ComponentLevel.NetAddExperience(message.Count, message.PlaySound);
                        player.CharacterData.Level = message.Level;
                    }
                );
                break;
            case CharacterDropPackage message:
                PlayerEvent(player => { player.DoDrop(); });
                break;
            case CharacterDragDropPackage message:
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
            case CharacterStatisticsPackage message:
                PlayerEvent(player =>
                    {
                        var statistics = new EntitySystem.TemplatesDatabase.ValuesDictionary();
                        statistics.ApplyOverridesUseMessagePack(message.Stat);
                        player.CharacterStats.Load(statistics);
                    }
                );
                break;
            case CharacterPositionPackage message:
                PlayerEvent(player =>
                    {
                        player.ComponentBody.ApplyAuthoritativePosition(message.Position, message.Velocity);
                    }
                );
                break;
        }
    }
}
