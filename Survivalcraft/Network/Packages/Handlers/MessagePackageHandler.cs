namespace Game.Network.Packages.Handlers;

public sealed class MessagePackageHandler : PackageHandlerBase<MessagePackage>
{
    public override void Handle(MessagePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(MessagePackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (package.PackageMessageMode)
        {
            case MessagePackage.MessageMode.BaseMessage:
                var gameWidgets = project.FindSubsystem<SubsystemGameWidgets>(true)!;
                const bool external = false;
                if (!isServer)
                {
                    gameWidgets.AddNetMessage(
                        package.Message,
                        package.PlayerName,
                        package.MessageType,
                        package.ToClients,
                        external);
                    break;
                }

                if (package.From == null)
                {
                    break;
                }

                if (package.Message.TrimStart().StartsWith('/'))
                {
                    Log.Warning(
                        $"Rejected command-like chat message from {package.From.PlayerData.Name}; " +
                        "commands must use CommandPackage.");
                    break;
                }

                package.PlayerName = package.From.PlayerData.Name;
                var flag = project.FindSubsystem<SubsystemPlayers>(true)!.NoMsgPlayerGuidList
                    .Contains(package.From.GUID.ToString());
                if (!flag)
                {
                    gameWidgets.AddNetMessage(
                        package.Message,
                        package.PlayerName,
                        package.MessageType,
                        package.ToClients,
                        external);
                    package.Except = package.From;
                    netNode.QueuePackage(package);
                }

                break;
            case MessagePackage.MessageMode.LargeMessage:
                if (isServer)
                {
                    break;
                }

                foreach (var player in project.FindSubsystem<SubsystemPlayers>(true)!.PlayersData)
                {
                    player.ComponentPlayer?.ComponentGui?.DisplayLargeMessage(
                        package.LargeText,
                        package.SmallText,
                        package.Duration,
                        package.Delay
                    );
                }

                break;
        }
    }
}
