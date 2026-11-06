using Game.Components.Behavior;

namespace Game.Network.Handling;

public class ComponentBehaviorPackageHandler : PackageHandlerBase<ComponentBehaviorPackage>
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

        if (context.Project is null)
        {
            return;
        }

        var project = context.Project;
        if (isServer &&
            (package.PackageEventType != ComponentBehaviorPackage.EventType.HumanRow ||
             context.Sender is null ||
             !project.FindSubsystem<SubsystemCharacters>(true)!.Characters.Any(player =>
                 ReferenceEquals(player.ControllerConnection, context.Sender) &&
                 player.ComponentCharacter?.Entity.EntityId == package.EntityId
             )))
        {
            return;
        }

        if (!isServer &&
            package is ClothingEffectPackage or CreatureWhalePlumePackage or
                CreatureRowingStatePackage or CreatureSoundPackage) { return; }

        project.FindEntityById(
            package.EntityId,
            entity =>
            {
                switch (package)
                {
                    case CreatureShapeshiftStatePackage shapeshift:
                        entity.FindComponent<ComponentShapeshifter>()?.ApplyShapeshift(shapeshift.StartTime);
                        break;
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
                    case CreatureRowingStatePackage rowing:
                        NetworkSender.SendToObservers(entity, rowing, context.Sender);
                        break;
                }
            }
        );
    }
}
