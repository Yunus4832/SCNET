namespace Game.Network.Packages.Handlers;

public sealed class ComponentSleepPackageHandler : PackageHandlerBase<ComponentSleepPackage>
{
    public override void Handle(ComponentSleepPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ComponentSleepPackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
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
                            netNode.QueuePackage(
                                new ComponentSleepPackage(
                                    sleep,
                                    ComponentSleepPackage.EventType.Sleep,
                                    package.AllowManualWakeup,
                                    false,
                                    reason2
                                )
                                {
                                    To = package.From
                                }
                            );
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
