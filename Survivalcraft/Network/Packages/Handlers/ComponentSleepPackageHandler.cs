namespace Game.Network.Packages.Handlers;

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
        if (context.IsServer && (context.Sender is null ||
            !project.FindSubsystem<SubsystemPlayers>(true)!.PlayersData.Any(player =>
                ReferenceEquals(player.Client, context.Sender) &&
                player.ComponentPlayer?.Entity.EntityId == package.EntityId)))
        {
            return;
        }

        project.FindEntityById(package.EntityId, e =>
            {
                var sleep = e.FindComponent<ComponentSleep>();
                if (sleep == null)
                {
                    return;
                }

                switch (package.Type)
                {
                    case ComponentSleepPackage.EventType.SleepRequest:
                        if (sleep.CanSleep(out var reason2))
                        {
                            sleep.Sleep(package.AllowManualWakeup);
                        }
                        else
                        {
                            var response = new ComponentSleepPackage(
                                    sleep,
                                    ComponentSleepPackage.EventType.Sleep,
                                    package.AllowManualWakeup,
                                    false,
                                    reason2
                                );
                            if (context.Sender is not null)
                            {
                                netNode.QueuePackage(response, PackageAudience.To(context.Sender));
                            }
                        }

                        break;
                    case ComponentSleepPackage.EventType.Sleep:
                        if (package.Result)
                        {
                            sleep.NetSleep(package.AllowManualWakeup);
                        }
                        else
                        {
                            var player = sleep.Entity.FindComponent<ComponentPlayer>();
                            player?.ComponentGui.DisplaySmallMessage(package.Reason, Color.White, false, true);
                        }

                        break;
                    case ComponentSleepPackage.EventType.WakeupRequest:
                        sleep.WakeUp();
                        break;
                    case ComponentSleepPackage.EventType.WakeUp:
                        sleep.NetWakeUp();
                        break;
                }
            }
        );
    }
}
