using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentSleepPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        project.FindEntityById(EntityId, e =>
        {
            var sleep = e.FindComponent<ComponentSleep>();
            if (sleep == null)
            {
                return;
            }

            switch (Type)
            {
                case EventType.SleepRequest:
                    if (sleep.CanSleep(out var reason2))
                    {
                        sleep.Sleep(AllowManualWakeup);
                    }
                    else
                    {
                        netNode.QueuePackage(
                            new ComponentSleepPackage(sleep, EventType.Sleep, AllowManualWakeup, false, reason2)
                                { To = From });
                    }

                    break;
                case EventType.Sleep:
                    if (Result)
                    {
                        sleep.NetSleep(AllowManualWakeup);
                    }
                    else
                    {
                        var player = sleep.Entity.FindComponent<ComponentPlayer>();
                        player?.ComponentGui.DisplaySmallMessage(Reason, Color.White, false, true);
                    }

                    break;
                case EventType.WakeupRequest:
                    sleep.WakeUp();
                    break;
                case EventType.WakeUp:
                    sleep.NetWakeUp();
                    break;
            }
        });
    }
}

public sealed class ComponentSleepPackageHandler : PackageHandlerBase<ComponentSleepPackage>
{
    public override void Handle(ComponentSleepPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ComponentSleepPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
