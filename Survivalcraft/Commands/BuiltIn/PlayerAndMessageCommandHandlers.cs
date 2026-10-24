using Game.Network;
using Game.Network.Transport;

namespace Game.Commands;

internal static class PlayerAndMessageCommandHandlers
{
    private const int _maximumMessageLength = 512;

    public static CommandResult UpdateOwnPlayerProfile(
        CommandContext context,
        UpdateOwnPlayerProfileCommand command
    )
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
            command.CharacterSex
        );
    }

    public static CommandResult UpdatePlayerProfile(
        CommandContext context,
        UpdatePlayerProfileCommand command
    )
    {
        if (context.Project is null)
        {
            return CommandResult.LocalizedFail(
                "player.project_required",
                "NoWorld_Message",
                "当前没有已加载的世界。"
            );
        }

        var players = context.Project.FindSubsystem<SubsystemCharacters>(true)!;
        var player = players.FindCharacterData(item => item.CharacterId == command.PlayerId);
        if (player is null)
        {
            return CommandResult.LocalizedFail(
                "player.not_found",
                "PlayerTargetMissing_Message",
                "目标玩家不存在。"
            );
        }

        return ApplyProfile(
            player,
            players,
            command.Name,
            command.SkinName,
            command.CharacterSex
        );
    }

    private static CommandResult ApplyProfile(
        CharacterData actor,
        SubsystemCharacters players,
        string requestedName,
        string skinName,
        CharacterSex characterSex
    )
    {
        string name;
        try
        {
            name = CharacterData.SanitizeName(requestedName.Trim());
        }
        catch (InvalidOperationException)
        {
            return RejectProfile(
                actor,
                "player.profile.invalid_name",
                "CharacterNameInvalid_Message",
                "角色名称不包含有效字符。"
            );
        }

        if (!CharacterData.VerifyName(name))
        {
            return RejectProfile(
                actor,
                "player.profile.invalid_name",
                "CharacterNameEmpty_Message",
                "角色名称不能为空。"
            );
        }

        if (!players.IsCharacterNameAvailable(name, actor.CharacterId))
        {
            return RejectProfile(
                actor,
                "player.profile.duplicate_name",
                "CharacterNameUsed_Message",
                "该角色名称已被使用。"
            );
        }

        if (!Enum.IsDefined(characterSex) ||
            string.IsNullOrWhiteSpace(skinName))
        {
            return RejectProfile(
                actor,
                "player.profile.invalid_appearance",
                "PlayerSkinInvalid_Message",
                "角色外观数据无效。"
            );
        }

        actor.Name = name;
        actor.CharacterSkinName = skinName;
        actor.CharacterSex = characterSex;
        CommonLib.Net.QueuePackage(
            CharacterPackageBuilder.Capture(actor, CharacterDataPackage.DataType.Modify),
            PackageAudience.Global
        );
        CommonLib.Net.QueuePackage(CharacterPackageBuilder.Capture(players), PackageAudience.Global);
        return CommandResult.LocalizedOk(
            "player.profile.updated",
            "PlayerProfileUpdated_Message",
            "角色资料已更新。"
        );
    }

    private static CommandResult RejectProfile(
        CharacterData player,
        string code,
        string messageKey,
        string message
    )
    {
        if (player.Client is not null)
        {
            var snapshot = CharacterPackageBuilder.Capture(
                player,
                CharacterDataPackage.DataType.Modify
            );
            CommonLib.Net.QueuePackage(snapshot, PackageAudience.To(player.Client));
        }

        return CommandResult.LocalizedFail(code, messageKey, message);
    }

    public static CommandResult SendChatMessage(
        CommandContext context,
        SendChatMessageCommand command
    )
    {
        if (context.Project is null || context.Principal.Subject is not { } subject)
        {
            return CommandResult.LocalizedFail("chat.subject_required", "ChatSubjectRequired_Message", "聊天需要已进入存档的主体。");
        }

        var session = PlayerSessionQuery.Find(subject.Address);
        if (!ReferenceEquals(
                context.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.Find(subject.Address),
                subject
            ) ||
            session is null ||
            !ReferenceEquals(session.Subject, subject) ||
            subject.Ban is not null)
        {
            return CommandResult.LocalizedFail("chat.subject_required", "ChatSubjectRequired_Message", "聊天需要已进入存档的主体。");
        }

        if (subject.IsMuted)
        {
            return CommandResult.LocalizedFail("chat.muted", "ChatMuted_Message", "当前主体已被禁言。");
        }

        var actor = session.CharacterData;

        if (!Enum.IsDefined(command.Channel))
        {
            return CommandResult.LocalizedFail(
                "chat.invalid_channel",
                "ChatChannelInvalid_Message",
                "消息频道无效。"
            );
        }

        var text = command.Content.Trim();
        if (text.Length == 0 || text.Length > _maximumMessageLength)
        {
            return CommandResult.LocalizedFail(
                "chat.invalid_length",
                "ChatContentInvalid_Message",
                "消息不能为空且不能超过 512 个字符。"
            );
        }

        if (text.StartsWith('/'))
        {
            return CommandResult.LocalizedFail(
                "chat.command_required",
                "ChatCommandRequired_Message",
                "指令必须通过指令系统执行。"
            );
        }

        IReadOnlyCollection<byte>? recipients = null;
        if (command.Channel is GameMessageChannel.Team)
        {
            if (context.Project!.FindSubsystem<SubsystemTeams>(true)!.Store.FindForPlayer(subject.Address) is not
                { } group)
            {
                return CommandResult.LocalizedFail(
                    "chat.team_required",
                    "TeamRequired_Message",
                    "你当前不在有效的队伍中。"
                );
            }

            recipients = CommonLib.Net.Clients.Values
                .Where(session => session.PlayerAddress is { } address && group.Members.Contains(address))
                .Select(session => session.ID).Distinct().ToArray();
        }

        var messages = context.Project!
            .FindSubsystem<SubsystemGameWidgets>(true)!
            .Messages;
        var message = messages.Normalize(
            GameMessage.Chat(
                command.Channel,
                new MessageSender(
                    subject.Address,
                    subject.DisplayName,
                    actor?.CharacterId,
                    actor?.Name ?? string.Empty
                ),
                text
            )
        );
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
        out CharacterData actor,
        out SubsystemCharacters players,
        out CommandResult failure
    )
    {
        if (context.Project is null ||
            context.Principal.Character is not { } player)
        {
            actor = null!;
            players = null!;
            failure = CommandResult.LocalizedFail(
                "player.required",
                "OnlinePlayerRequired_Message",
                "该操作需要在线玩家。"
            );
            return false;
        }

        actor = player;
        players = context.Project.FindSubsystem<SubsystemCharacters>(true)!;
        if (!players.Characters.Contains(actor))
        {
            failure = CommandResult.LocalizedFail(
                "player.not_loaded",
                "PlayerNotLoaded_Message",
                "玩家尚未加载到当前世界。"
            );
            return false;
        }

        failure = null!;
        return true;
    }
}
