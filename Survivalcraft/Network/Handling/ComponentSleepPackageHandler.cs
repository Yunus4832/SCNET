using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ComponentSleepPackageHandler : PackageHandlerBase<ComponentSleepPackage>
{
    internal static bool AcceptsDirection(ComponentSleepPackage.EventType type, bool isServer)
    {
        return isServer
            ? type is ComponentSleepPackage.EventType.SleepRequest or ComponentSleepPackage.EventType.WakeupRequest
            : type is ComponentSleepPackage.EventType.Sleep or ComponentSleepPackage.EventType.WakeUp;
    }

    public override void Handle(ComponentSleepPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ComponentSleepPackage)}");
            return;
        }

        if (GameManager.Project is null || !AcceptsDirection(package.Type, context.IsServer))
        {
            return;
        }

        var project = GameManager.Project;
        if (context.IsServer &&
            (context.Sender is null ||
             !project.FindSubsystem<SubsystemCharacters>(true)!.Characters.Any(player =>
                 ReferenceEquals(player.Client, context.Sender) &&
                 player.ComponentCharacter?.Entity.EntityId == package.EntityId
             )))
        {
            return;
        }

        project.FindEntityById(
            package.EntityId,
            e =>
            {
                var sleep = e.FindComponent<ComponentSleep>();
                if (sleep == null)
                {
                    return;
                }

                switch (package)
                {
                    case SleepRequestPackage message1:
                    {
                        if (sleep.CanSleep(out var reason2))
                        {
                            sleep.Sleep(message1.AllowManualWakeup);
                        }
                        else
                        {
                            var response = new SleepResultPackage
                            {
                                Reason = reason2, EntityId = sleep.Entity.EntityId,
                                AllowManualWakeup = message1.AllowManualWakeup, Result = false
                            };
                            if (context.Sender is not null)
                            {
                                netNode.QueuePackage(response, PackageAudience.To(context.Sender));
                            }
                        }

                        break;
                    }
                    case SleepResultPackage message2:
                    {
                        if (message2.Result)
                        {
                            sleep.NetSleep(message2.AllowManualWakeup);
                        }
                        else
                        {
                            var player = sleep.Entity.FindComponent<ComponentCharacter>();
                            player?.ComponentGui.DisplaySmallMessage(message2.Reason, Color.White, false, true);
                        }

                        break;
                    }
                    case WakeupRequestPackage message3:
                    {
                        sleep.WakeUp();
                        break;
                    }
                    case WakeupPackage message4:
                    {
                        sleep.NetWakeUp();
                        break;
                    }
                }
            }
        );
    }
}
