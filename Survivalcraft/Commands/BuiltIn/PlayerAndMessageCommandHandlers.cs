using Game.Messaging;
using Game.Network;
using Game.Network.Packages;

namespace Game.Commands;

internal static class PlayerAndMessageCommandHandlers
{
    private const int _maximumMessageLength = 512;

    public static CommandResult UpdateOwnPlayerProfile(
        CommandContext context,
        UpdateOwnPlayerProfileCommand command)
    {
        if (!TryGetActor(context, out var actor, out var players, out var failure))
        {
            return failure;
        }

        return ApplyProfile(
            actor,
            players,
            command.Name,
            command.SkinName,
            command.PlayerClass,
            enforceConnectionNickname: true);
    }

    public static CommandResult UpdatePlayerProfile(
        CommandContext context,
        UpdatePlayerProfileCommand command)
    {
        if (context.Project is null)
        {
            return CommandResult.LocalizedFail(
                "player.project_required",
                "NoWorld_Message",
                "当前没有已加载的世界。");
        }

        var players = context.Project.FindSubsystem<SubsystemPlayers>(true)!;
        var player = players.FindPlayerData(
            item => item.PlayerGUID == command.PlayerId);
        if (player is null)
        {
            return CommandResult.LocalizedFail(
                "player.not_found",
                "PlayerTargetMissing_Message",
                "目标玩家不存在。");
        }

        return ApplyProfile(
            player,
            players,
            command.Name,
            command.SkinName,
            command.PlayerClass,
            enforceConnectionNickname: false);
    }

    private static CommandResult ApplyProfile(
        PlayerData actor,
        SubsystemPlayers players,
        string requestedName,
        string skinName,
        PlayerClass playerClass,
        bool enforceConnectionNickname)
    {
        string name;
        try
        {
            name = PlayerData.SanitizeName(requestedName.Trim());
        }
        catch (InvalidOperationException)
        {
            return RejectProfile(
                actor,
                "player.profile.invalid_name",
                "PlayerNameInvalid_Message",
                "玩家名称不包含有效字符。");
        }

        if (!PlayerData.VerifyName(name))
        {
            return RejectProfile(
                actor,
                "player.profile.invalid_name",
                "PlayerNameEmpty_Message",
                "玩家名称不能为空。");
        }

        if (enforceConnectionNickname &&
            actor.Client is { Nickname.Length: > 0 } client)
        {
            name = client.Nickname;
        }
        else if (players.PlayersData.Any(
                     player => player != actor &&
                               string.Equals(player.Name, name, StringComparison.Ordinal)))
        {
            return RejectProfile(
                actor,
                "player.profile.duplicate_name",
                "PlayerNameUsed_Message",
                "该玩家名称已被使用。");
        }

        if (!Enum.IsDefined(playerClass) ||
            string.IsNullOrWhiteSpace(skinName))
        {
            return RejectProfile(
                actor,
                "player.profile.invalid_appearance",
                "PlayerSkinInvalid_Message",
                "玩家外观数据无效。");
        }

        actor.Name = name;
        actor.CharacterSkinName = skinName;
        actor.PlayerClass = playerClass;
        CommonLib.Net.QueuePackage(
            new PlayerDataPackage(actor, PlayerDataPackage.DataType.Modify));
        CommonLib.Net.QueuePackage(new PlayerListPackage(players));
        return CommandResult.LocalizedOk(
            "player.profile.updated",
            "PlayerProfileUpdated_Message",
            "玩家资料已更新。");
    }

    private static CommandResult RejectProfile(
        PlayerData player,
        string code,
        string messageKey,
        string message)
    {
        if (player.Client is not null)
        {
            var snapshot = new PlayerDataPackage(
                player,
                PlayerDataPackage.DataType.Modify);
            snapshot.To = player.Client;
            CommonLib.Net.QueuePackage(snapshot);
        }

        return CommandResult.LocalizedFail(code, messageKey, message);
    }

    public static CommandResult SendChatMessage(
        CommandContext context,
        SendChatMessageCommand command)
    {
        if (!TryGetActor(context, out var actor, out var players, out var failure))
        {
            return failure;
        }

        if (!Enum.IsDefined(command.Channel))
        {
            return CommandResult.LocalizedFail(
                "chat.invalid_channel",
                "ChatChannelInvalid_Message",
                "消息频道无效。");
        }

        var text = command.Content.Trim();
        if (text.Length == 0 || text.Length > _maximumMessageLength)
        {
            return CommandResult.LocalizedFail(
                "chat.invalid_length",
                "ChatContentInvalid_Message",
                "消息不能为空且不能超过 512 个字符。");
        }

        if (text.StartsWith('/'))
        {
            return CommandResult.LocalizedFail(
                "chat.command_required",
                "ChatCommandRequired_Message",
                "指令必须通过指令系统执行。");
        }

        IReadOnlyCollection<byte>? recipients = null;
        if (command.Channel is GameMessageChannel.Team)
        {
            if (actor.GroupKey.Length == 0 ||
                !players.ServerGroups.TryGetValue(actor.GroupKey, out var group))
            {
                return CommandResult.LocalizedFail(
                    "chat.team_required",
                    "TeamRequired_Message",
                    "你当前不在有效的队伍中。");
            }

            recipients = group.Members
                .Select(member => players.FindPlayerData(
                    item => item.PlayerGUID == member))
                .Where(item => item?.Client is not null || item?.IsMainPlayer == true)
                .Select(item => item!.ClientId)
                .Distinct()
                .ToArray();
        }

        var messages = context.Project!
            .FindSubsystem<SubsystemGameWidgets>(true)!
            .Messages;
        var message = messages.Normalize(
            GameMessage.Chat(command.Channel, actor.Name, text));
        var shouldDisplayOnServer =
            RunMode.Value is RunModeType.HeadlessServer ||
            recipients is null ||
            CommonLib.Net.Self is { } self && recipients.Contains(self.ID);
        if (shouldDisplayOnServer)
        {
            messages.Receive(message);
        }

        messages.Relay(message, recipients);
        return CommandResult.SilentOk("chat.sent");
    }

    private static bool TryGetActor(
        CommandContext context,
        out PlayerData actor,
        out SubsystemPlayers players,
        out CommandResult failure)
    {
        if (context.Project is null ||
            context.Principal.Player is not { } player)
        {
            actor = null!;
            players = null!;
            failure = CommandResult.LocalizedFail(
                "player.required",
                "OnlinePlayerRequired_Message",
                "该操作需要在线玩家。");
            return false;
        }

        actor = player;
        players = context.Project.FindSubsystem<SubsystemPlayers>(true)!;
        if (!players.PlayersData.Contains(actor))
        {
            failure = CommandResult.LocalizedFail(
                "player.not_loaded",
                "PlayerNotLoaded_Message",
                "玩家尚未加载到当前世界。");
            return false;
        }

        failure = null!;
        return true;
    }
}
