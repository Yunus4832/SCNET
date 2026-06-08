using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class MessagePackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (PackageMessageMode)
        {
            case MessageMode.BaseMessage:
                var gameWidgets = project.FindSubsystem<SubsystemGameWidgets>(true)!;
                const bool external = false;
                gameWidgets.AddNetMessage(Message, PlayerName, MessageType, ToClients, external);
                if (!isServer || From == null)
                {
                    break;
                }

                PlayerName = From.PlayerData.Name;
                var flag = project.FindSubsystem<SubsystemPlayers>(true)!.NoMsgPlayerGuidList
                    .Contains(From.GUID.ToString());
                if (!flag)
                {
                    Except = From;
                    netNode.QueuePackage(this);
                }

                break;
            case MessageMode.LargeMessage:
                if (isServer)
                {
                    break;
                }

                foreach (var player in project.FindSubsystem<SubsystemPlayers>(true)!.PlayersData)
                {
                    player.ComponentPlayer?.ComponentGui?.DisplayLargeMessage(
                        LargeText,
                        SmallText,
                        Duration,
                        Delay
                    );
                }

                break;
        }
    }
}

public sealed class MessagePackageHandler : PackageHandlerBase<MessagePackage>
{
    public override void Handle(MessagePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(MessagePackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
