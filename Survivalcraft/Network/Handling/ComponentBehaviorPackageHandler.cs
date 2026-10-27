using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ComponentBehaviorPackageHandler : PackageHandlerBase<ComponentBehaviorPackage>
{
    public override void Handle(ComponentBehaviorPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ComponentBehaviorPackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        if (isServer &&
            (package.PackageEventType != ComponentBehaviorPackage.EventType.HumanRow ||
             context.Sender is null ||
             !project.FindSubsystem<SubsystemCharacters>(true)!.Characters.Any(player =>
                 ReferenceEquals(player.Client, context.Sender) &&
                 player.ComponentCharacter?.Entity.EntityId == package.EntityId
             )))
        {
            return;
        }

        project.FindEntityById(
            package.EntityId,
            entity =>
            {
                switch (package)
                {
                    case CreatureChaseStatePackage message1:
                    {
                        var chaseBehavior = entity.FindComponent<ComponentChaseBehavior>();
                        chaseBehavior?.IsAttack = message1.IsActive;

                        break;
                    }
                    case CreatureFeedStatePackage message2:
                    {
                        var randomFeedBehavior = entity.FindComponent<ComponentRandomFeedBehavior>();
                        randomFeedBehavior?.IsFeed = message2.IsActive;

                        break;
                    }
                    case CreaturePeckStatePackage message3:
                    {
                        var peck = entity.FindComponent<ComponentRandomPeckBehavior>();
                        peck?.IsFeed = message3.IsActive;

                        break;
                    }
                    case CreatureEatStatePackage message4:
                    {
                        var eatPickableBehavior = entity.FindComponent<ComponentEatPickableBehavior>();
                        eatPickableBehavior?.IsFeed = message4.IsActive;
                        break;
                    }
                    case CreatureDigStatePackage message5:
                    {
                        var digInMudBehavior = entity.FindComponent<ComponentDigInMudBehavior>();
                        digInMudBehavior?.IsDigIn = message5.IsActive;
                        break;
                    }
                    case CreatureOutOfWaterStatePackage message6:
                    {
                        var fishOutOfWaterBehavior = entity.FindComponent<ComponentFishOutOfWaterBehavior>();
                        fishOutOfWaterBehavior?.IsBend = message6.IsActive;
                        break;
                    }
                    case CreatureRowingStatePackage message7:
                    {
                        var humanModel = entity.FindComponent<ComponentHumanModel>();
                        if (humanModel != null)
                        {
                            humanModel.HasData = true;
                            humanModel.RowLeft = message7.RowLeft;
                            humanModel.RowRight = message7.RowRight;
                            var random = new Random();
                            project.FindSubsystem<SubsystemAudio>(true)!.PlayRandomSound(
                                "Audio/Rowing",
                                random.Float(0.4f, 0.6f),
                                random.Float(-0.3f, 0.2f),
                                humanModel.ComponentCreature.ComponentBody.Position,
                                3f,
                                true
                            );
                            if (isServer)
                            {
                                NetworkSender.SendToObservers(entity, message7, context.Sender);
                            }
                        }

                        break;
                    }
                    case CreatureSoundPackage message8:
                    {
                        var creatureSound = entity.FindComponent<ComponentCreatureSounds>();
                        if (creatureSound != null)
                        {
                            switch (message8.SoundKind)
                            {
                                case 0:
                                    creatureSound.PlayIdleSoundLocal(message8.SkipIfRecentlyPlayed);
                                    break;
                                case 1:
                                    creatureSound.PlayPainSoundLocal();
                                    break;
                                case 2:
                                    creatureSound.PlayMoanSoundLocal();
                                    break;
                                case 3:
                                    creatureSound.PlaySneezeSoundLocal();
                                    break;
                                case 4:
                                    creatureSound.PlayCoughSoundLocal();
                                    break;
                                case 5:
                                    creatureSound.PlayPukeSoundLocal();
                                    break;
                                case 6:
                                    creatureSound.PlayAttackSoundLocal();
                                    break;
                            }
                        }

                        break;
                    }
                }
            }
        );
    }
}
