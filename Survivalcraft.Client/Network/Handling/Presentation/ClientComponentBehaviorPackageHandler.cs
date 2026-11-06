using Game.Network.Transport;

namespace Game.Network.Handling.Presentation;

public sealed class ClientComponentBehaviorPackageHandler : ComponentBehaviorPackageHandler
{
    public override void Handle(ComponentBehaviorPackage package, PackageReceiveContext context)
    {
        base.Handle(package, context);
        if (context.Node is null ||
            context.IsServer ||
            context.Runtime.IsClosed ||
            !context.Runtime.HasPresentation ||
            context.Project is not { } project ||
            package is not (ClothingEffectPackage or CreatureWhalePlumePackage or
                CreatureRowingStatePackage or CreatureSoundPackage)) { return; }

        project.FindEntityById(
            package.EntityId,
            entity =>
            {
                switch (package)
                {
                    case ClothingEffectPackage clothing:
                        entity.FindComponent<ComponentClothingPresentation>()?.ApplyEffect(clothing);
                        break;
                    case CreatureWhalePlumePackage plume:
                        entity.FindComponent<ComponentCetaceanBreathePresentation>()?.ApplyPlume(plume);
                        break;
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
                        }

                        break;
                    }
                    case CreatureSoundPackage message8:
                    {
                        var creatureSound = entity.FindComponent<ComponentCreatureSoundsPresentation>();
                        if (creatureSound != null)
                        {
                            switch (message8.SoundKind)
                            {
                                case 0:
                                    creatureSound.PlayIdleSound(message8.SkipIfRecentlyPlayed);
                                    break;
                                case 1:
                                    creatureSound.PlayPainSound();
                                    break;
                                case 2:
                                    creatureSound.PlayMoanSound();
                                    break;
                                case 3:
                                    creatureSound.PlaySneezeSound();
                                    break;
                                case 4:
                                    creatureSound.PlayCoughSound();
                                    break;
                                case 5:
                                    creatureSound.PlayPukeSound();
                                    break;
                                case 6:
                                    creatureSound.PlayAttackSound();
                                    break;
                                case 7:
                                    creatureSound.PlayFootstepSound(2f);
                                    break;
                                case 8:
                                    creatureSound.PlayHowlSound();
                                    break;
                                case 9:
                                    creatureSound.PlayWingsSound();
                                    break;
                                case 10:
                                    creatureSound.PlayEggLaidSound();
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
