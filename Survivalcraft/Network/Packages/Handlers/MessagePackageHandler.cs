using Game.Messaging;

namespace Game.Network.Packages.Handlers;

public sealed class MessagePackageHandler : PackageHandlerBase<MessagePackage>
{
    private const int _maximumMessageLength = 512;

    public override void Handle(MessagePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode is null || GameManager.Project is not { } project)
        {
            return;
        }

        var messages = project.FindSubsystem<SubsystemGameWidgets>(true)!.Messages;
        if (!isServer)
        {
            messages.Receive(package.GameMessage);
            return;
        }

        if (package.From is null ||
            package.GameMessage.Kind is not GameMessageKind.Chat ||
            !Enum.IsDefined(package.GameMessage.Channel))
        {
            return;
        }

        var text = package.GameMessage.Content.PlainText.Trim();
        if (text.Length == 0 || text.Length > _maximumMessageLength)
        {
            SendError(netNode, package.From, "消息不能为空且不能超过 512 个字符。");
            return;
        }

        if (text.StartsWith('/'))
        {
            SendError(netNode, package.From, "指令必须通过指令系统执行。");
            return;
        }

        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        var player = package.From.PlayerData;
        IReadOnlyCollection<byte>? recipients = null;
        if (package.GameMessage.Channel is GameMessageChannel.Team)
        {
            if (player.GroupKey.Length == 0 ||
                !subsystemPlayers.ServerGroups.TryGetValue(player.GroupKey, out var group))
            {
                SendError(netNode, package.From, "你当前不在有效的队伍中。");
                return;
            }

            recipients = group.Members
                .Select(member => subsystemPlayers.FindPlayerData(
                    item => item.PlayerGUID == member))
                .Where(item => item?.Client is not null || item?.IsMainPlayer == true)
                .Select(item => item!.ClientId)
                .Distinct()
                .ToArray();
        }

        var message = messages.Normalize(GameMessage.Chat(
            package.GameMessage.Channel,
            player.Name,
            text));
        var shouldDisplayOnServer =
            RunMode.Value is RunModeType.HeadlessServer ||
            recipients is null ||
            CommonLib.Net.Self is { } self && recipients.Contains(self.ID);
        if (shouldDisplayOnServer)
        {
            messages.Receive(message);
        }

        messages.Relay(message, recipients);
    }

    private static void SendError(NetNode netNode, Client client, string text)
    {
        netNode.QueuePackage(new MessagePackage(
            GameMessage.System(text, GameMessageTone.Error))
        {
            To = client
        });
    }
}
