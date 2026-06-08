namespace Game.Network.Packages.Handlers;

public sealed class ComponentBehaviorPackageHandler : PackageHandlerBase<ComponentBehaviorPackage>
{
    public override void Handle(ComponentBehaviorPackage package, NetNode? netNode, bool isServer)
    {
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
        project.FindEntityById(package.EntityId, entity =>
        {
            switch (package.PackageEventType)
            {
                case ComponentBehaviorPackage.EventType.ChaseBehavior:
                    var chaseBehavior = entity.FindComponent<ComponentChaseBehavior>();
                    chaseBehavior?.IsAttack = package.RowLeft;

                    break;
                case ComponentBehaviorPackage.EventType.RandomFeed:
                    var randomFeedBehavior = entity.FindComponent<ComponentRandomFeedBehavior>();
                    randomFeedBehavior?.IsFeed = package.RowLeft;

                    break;

                case ComponentBehaviorPackage.EventType.RandomPeck:
                    var peck = entity.FindComponent<ComponentRandomPeckBehavior>();
                    peck?.IsFeed = package.RowLeft;

                    break;
                case ComponentBehaviorPackage.EventType.EatPickable:
                    var eatPickableBehavior = entity.FindComponent<ComponentEatPickableBehavior>();
                    eatPickableBehavior?.IsFeed = package.RowLeft;
                    break;
                case ComponentBehaviorPackage.EventType.DigInMud:
                    var digInMudBehavior = entity.FindComponent<ComponentDigInMudBehavior>();
                    digInMudBehavior?.IsDigIn = package.RowLeft;
                    break;
                case ComponentBehaviorPackage.EventType.FishOutOfWater:
                    var fishOutOfWaterBehavior = entity.FindComponent<ComponentFishOutOfWaterBehavior>();
                    fishOutOfWaterBehavior?.IsBend = package.RowLeft;
                    break;
                case ComponentBehaviorPackage.EventType.HumanRow:
                    var humanModel = entity.FindComponent<ComponentHumanModel>();
                    if (humanModel != null)
                    {
                        humanModel.HasData = true;
                        humanModel.RowLeft = package.RowLeft;
                        humanModel.RowRight = package.RowRight;
                        var random = new Random();
                        project.FindSubsystem<SubsystemAudio>(true)!.PlayRandomSound("Audio/Rowing",
                            random.Float(0.4f, 0.6f), random.Float(-0.3f, 0.2f),
                            humanModel.ComponentCreature.ComponentBody.Position, 3f, true);
                        if (isServer)
                        {
                            package.Except = package.From;
                            netNode.QueuePackage(package);
                        }
                    }

                    break;
                case ComponentBehaviorPackage.EventType.CreatureSound:
                    var creatureSound = entity.FindComponent<ComponentCreatureSounds>();
                    if (creatureSound != null)
                    {
                        switch (package.Type)
                        {
                            case 0:
                                creatureSound.PlayIdleSoundLogic(package.RowLeft);
                                break;
                            case 1:
                                creatureSound.PlayPainSoundLogic();
                                break;
                            case 2:
                                creatureSound.PlayMoanSoundLogic();
                                break;
                            case 3:
                                creatureSound.PlaySneezeSoundLogic();
                                break;
                            case 4:
                                creatureSound.PlayCoughSoundLogic();
                                break;
                            case 5:
                                creatureSound.PlayPukeSoundLogic();
                                break;
                            case 6:
                                creatureSound.PlayAttackSoundLogic();
                                break;
                        }
                    }

                    break;
            }
        });
    }
}
