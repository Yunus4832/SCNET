namespace Game.Network.Handling;

public class ComponentSleepPackageHandler : PackageHandlerBase<ComponentSleepPackage>
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

        if (context.Project is null || !AcceptsDirection(package.Type, context.IsServer))
        {
            return;
        }

        var project = context.Project;
        if (!context.IsServer && package is SleepResultPackage { Result: false }) { return; }

        if (context.IsServer &&
            (context.Sender is null ||
             !project.FindSubsystem<SubsystemCharacters>(true)!.Characters.Any(player =>
                 ReferenceEquals(player.ControllerConnection, context.Sender) &&
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
                        if (sleep.IsSleeping) { return; }

                        if (sleep.CanSleep(out var reason2))
                        {
                            sleep.Sleep(true);
                        }
                        else
                        {
                            var response = new SleepResultPackage
                            {
                                Reason = reason2, EntityId = sleep.Entity.EntityId,
                                Result = false
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
                            sleep.ApplySleepState(message2.StartTime, message2.AllowManualWakeup);
                        }

                        break;
                    }
                    case WakeupRequestPackage message3:
                    {
                        if (sleep.AcceptsWakeUpRequest(message3.SleepStartTime))
                        {
                            sleep.WakeUp(SleepWakeReason.Manual);
                        }

                        break;
                    }
                    case WakeupPackage message4:
                    {
                        sleep.ApplySleepState(null, false, message4.Reason);
                        break;
                    }
                }
            }
        );
    }
}
