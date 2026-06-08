using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentBehaviorPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        project.FindEntityById(EntityId, entity =>
        {
            switch (PackageEventType)
            {
                case EventType.ChaseBehavior:
                    var chaseBehavior = entity.FindComponent<ComponentChaseBehavior>();
                    chaseBehavior?.IsAttack = RowLeft;

                    break;
                case EventType.RandomFeed:
                    var randomFeedBehavior = entity.FindComponent<ComponentRandomFeedBehavior>();
                    randomFeedBehavior?.IsFeed = RowLeft;

                    break;

                case EventType.RandomPeck:
                    var peck = entity.FindComponent<ComponentRandomPeckBehavior>();
                    peck?.IsFeed = RowLeft;

                    break;
                case EventType.EatPickable:
                    var eatPickableBehavior = entity.FindComponent<ComponentEatPickableBehavior>();
                    eatPickableBehavior?.IsFeed = RowLeft;
                    break;
                case EventType.DigInMud:
                    var digInMudBehavior = entity.FindComponent<ComponentDigInMudBehavior>();
                    digInMudBehavior?.IsDigIn = RowLeft;
                    break;
                case EventType.FishOutOfWater:
                    var fishOutOfWaterBehavior = entity.FindComponent<ComponentFishOutOfWaterBehavior>();
                    fishOutOfWaterBehavior?.IsBend = RowLeft;
                    break;
                case EventType.HumanRow:
                    var humanModel = entity.FindComponent<ComponentHumanModel>();
                    if (humanModel != null)
                    {
                        humanModel.HasData = true;
                        humanModel.RowLeft = RowLeft;
                        humanModel.RowRight = RowRight;
                        var random = new Random();
                        project.FindSubsystem<SubsystemAudio>(true)!.PlayRandomSound("Audio/Rowing",
                            random.Float(0.4f, 0.6f), random.Float(-0.3f, 0.2f),
                            humanModel.ComponentCreature.ComponentBody.Position, 3f, true);
                        if (isServer)
                        {
                            Except = From;
                            netNode.QueuePackage(this);
                        }
                    }

                    break;
                case EventType.CreatureSound:
                    var creatureSound = entity.FindComponent<ComponentCreatureSounds>();
                    if (creatureSound != null)
                    {
                        switch (Type)
                        {
                            case 0:
                                creatureSound.PlayIdleSoundLogic(RowLeft);
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

public sealed class ComponentBehaviorPackageHandler : PackageHandlerBase<ComponentBehaviorPackage>
{
    public override void Handle(ComponentBehaviorPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ComponentBehaviorPackage)}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
