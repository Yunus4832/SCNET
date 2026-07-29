namespace Game.Subsystems;

public partial class SubsystemPlayers
{
    public enum PendingGroupOperationKind
    {
        JoinRequest,
        Invitation
    }

    public sealed record PendingGroupOperation(
        Guid OperationId,
        PendingGroupOperationKind Kind,
        Guid Initiator,
        Guid Responder,
        Guid JoiningPlayer,
        Guid GroupKey,
        double ExpiresAt);

    public sealed record GroupOperationMessage(
        string Key,
        string Fallback,
        IReadOnlyList<string> Arguments)
    {
        public static GroupOperationMessage Create(
            string key,
            string fallback,
            params string[] arguments)
        {
            return new GroupOperationMessage(key, fallback, arguments);
        }
    }

    private const double _groupOperationLifetime = 60.0;

    private const double _groupOperationMinimumInterval = 1.0;

    private readonly Dictionary<Guid, double> _lastGroupOperationTimes = [];

    private readonly Dictionary<Guid, PendingGroupOperation> _pendingGroupOperations = [];

    public bool TryStartGroupOperation(
        PlayerData actor,
        out GroupOperationMessage? error)
    {
        CleanupExpiredGroupOperations();
        var now = Time.RealTime;
        if (_lastGroupOperationTimes.TryGetValue(actor.PlayerGUID, out var lastTime) &&
            now - lastTime < _groupOperationMinimumInterval)
        {
            error = GroupOperationMessage.Create(
                "TeamRateLimited_Message",
                "操作过于频繁，请稍后重试。");
            return false;
        }

        _lastGroupOperationTimes[actor.PlayerGUID] = now;
        error = null;
        return true;
    }

    public bool TryCreateGroup(
        PlayerData actor,
        string requestedName,
        out GroupOperationMessage message)
    {
        var name = requestedName.Trim();
        if (name.Length is < 1 or > 64 || name.Any(char.IsControl))
        {
            message = GroupOperationMessage.Create(
                "TeamNameInvalid_Message",
                "队伍名称必须为 1 到 64 个有效字符。");
            return false;
        }

        if (GetPlayerGroupKey(actor.PlayerGUID).Length > 0 || actor.GroupKey.Length > 0)
        {
            message = GroupOperationMessage.Create(
                "TeamAlreadyMember_Message",
                "你已经在队伍中。");
            return false;
        }

        var key = actor.PlayerGUID.ToString();
        if (ServerGroups.ContainsKey(key))
        {
            message = GroupOperationMessage.Create(
                "TeamDuplicate_Message",
                "无法创建重复的队伍。");
            return false;
        }

        var group = new Group { Name = name };
        group.Members.Add(actor.PlayerGUID);
        ServerGroups.Add(key, group);
        actor.GroupKey = key;
        message = GroupOperationMessage.Create(
            "TeamCreated_Message",
            "已创建队伍“{0}”。",
            name);
        return true;
    }

    public bool TryLeaveGroup(PlayerData actor, out GroupOperationMessage message)
    {
        var groupKey = GetPlayerGroupKey(actor.PlayerGUID);
        if (groupKey.Length == 0 ||
            !ServerGroups.TryGetValue(groupKey, out var group))
        {
            actor.GroupKey = string.Empty;
            message = GroupOperationMessage.Create(
                "TeamNotMember_Message",
                "你当前不在队伍中。");
            return false;
        }

        if (groupKey == actor.PlayerGUID.ToString())
        {
            foreach (var member in group.Members)
            {
                var player = FindPlayerData(item => item.PlayerGUID == member);
                player?.GroupKey = string.Empty;
            }

            ServerGroups.Remove(groupKey);
            RemovePendingOperationsForGroup(actor.PlayerGUID);
            message = GroupOperationMessage.Create(
                "TeamDisbanded_Message",
                "队长退出，队伍已解散。");
            return true;
        }

        group.Members.Remove(actor.PlayerGUID);
        actor.GroupKey = string.Empty;
        RemovePendingOperationsForPlayer(actor.PlayerGUID);
        message = GroupOperationMessage.Create(
            "TeamLeft_Message",
            "已退出队伍“{0}”。",
            group.Name);
        return true;
    }

    public bool TryCreateJoinRequest(
        PlayerData requester,
        Guid groupKey,
        out PendingGroupOperation? operation,
        out PlayerData? responder,
        out GroupOperationMessage message)
    {
        operation = null;
        responder = null;
        if (!TryValidateCanJoin(requester, groupKey, out var group, out message))
        {
            return false;
        }

        if (!group!.Members.Contains(groupKey))
        {
            message = GroupOperationMessage.Create(
                "TeamStateInvalid_Message",
                "队伍状态无效，无法处理加入申请。");
            return false;
        }

        if (HasPendingOperationForPlayer(requester.PlayerGUID))
        {
            message = GroupOperationMessage.Create(
                "TeamRequestPending_Message",
                "你已经有一个待处理的队伍请求。");
            return false;
        }

        responder = FindPlayerData(player => player.PlayerGUID == groupKey);
        if (responder is null || responder.Client is null && !responder.IsMainPlayer)
        {
            message = GroupOperationMessage.Create(
                "TeamLeaderOffline_Message",
                "队长当前不在线，无法处理加入申请。");
            return false;
        }

        operation = AddPendingOperation(
            PendingGroupOperationKind.JoinRequest,
            requester.PlayerGUID,
            responder.PlayerGUID,
            requester.PlayerGUID,
            groupKey);
        message = GroupOperationMessage.Create(
            "TeamJoinSent_Message",
            "已向队伍“{0}”发送加入申请。",
            group!.Name);
        return true;
    }

    public bool TryCreateInvitation(
        PlayerData inviter,
        Guid groupKey,
        Guid targetPlayer,
        out PendingGroupOperation? operation,
        out PlayerData? responder,
        out GroupOperationMessage message)
    {
        operation = null;
        responder = null;
        if (!ServerGroups.TryGetValue(groupKey.ToString(), out var group) ||
            !group.Members.Contains(inviter.PlayerGUID))
        {
            message = GroupOperationMessage.Create(
                "TeamNotMember_Message",
                "你不是该队伍的成员。");
            return false;
        }

        responder = FindPlayerData(player => player.PlayerGUID == targetPlayer);
        if (responder is null || responder.Client is null && !responder.IsMainPlayer)
        {
            message = GroupOperationMessage.Create(
                "TeamTargetOffline_Message",
                "目标玩家当前不在线。");
            return false;
        }

        if (GetPlayerGroupKey(targetPlayer).Length > 0 || responder.GroupKey.Length > 0)
        {
            message = GroupOperationMessage.Create(
                "TeamTargetAlreadyMember_Message",
                "目标玩家已经在队伍中。");
            return false;
        }

        if (HasPendingOperationForPlayer(targetPlayer))
        {
            message = GroupOperationMessage.Create(
                "TeamTargetPending_Message",
                "目标玩家已经有一个待处理的队伍请求。");
            return false;
        }

        operation = AddPendingOperation(
            PendingGroupOperationKind.Invitation,
            inviter.PlayerGUID,
            responder.PlayerGUID,
            responder.PlayerGUID,
            groupKey);
        message = GroupOperationMessage.Create(
            "TeamInvitationSent_Message",
            "已向 {0} 发送队伍邀请。",
            responder.Name);
        return true;
    }

    public bool TryRespondToGroupOperation(
        PlayerData responder,
        Guid operationId,
        bool accepted,
        out PendingGroupOperation? operation,
        out GroupOperationMessage message)
    {
        CleanupExpiredGroupOperations();
        if (!_pendingGroupOperations.TryGetValue(operationId, out operation) ||
            operation.Responder != responder.PlayerGUID)
        {
            operation = null;
            message = GroupOperationMessage.Create(
                "TeamRequestExpired_Message",
                "该队伍请求不存在或已经失效。");
            return false;
        }

        _pendingGroupOperations.Remove(operationId);
        if (!accepted)
        {
            message = GroupOperationMessage.Create(
                "TeamRequestRejected_Message",
                "已拒绝队伍请求。");
            return true;
        }

        var joiningPlayerGuid = operation.JoiningPlayer;
        var joiningPlayer = FindPlayerData(player => player.PlayerGUID == joiningPlayerGuid);
        if (joiningPlayer is not null)
        {
            return TryJoinGroup(joiningPlayer, operation.GroupKey, out message);
        }

        message = GroupOperationMessage.Create(
            "TeamJoiningPlayerOffline_Message",
            "申请加入的玩家已经离线。");
        return false;
    }

    private bool TryJoinGroup(
        PlayerData player,
        Guid groupKey,
        out GroupOperationMessage message)
    {
        if (!TryValidateCanJoin(player, groupKey, out var group, out message))
        {
            return false;
        }

        group!.Members.Add(player.PlayerGUID);
        player.GroupKey = groupKey.ToString();
        RemovePendingOperationsForPlayer(player.PlayerGUID);
        message = GroupOperationMessage.Create(
            "TeamJoined_Message",
            "已加入队伍“{0}”。",
            group.Name);
        return true;
    }

    private bool TryValidateCanJoin(
        PlayerData player,
        Guid groupKey,
        out Group? group,
        out GroupOperationMessage message)
    {
        group = null;
        var currentGroup = GetPlayerGroupKey(player.PlayerGUID);
        if (currentGroup.Length > 0 || player.GroupKey.Length > 0)
        {
            message = GroupOperationMessage.Create(
                "TeamPlayerAlreadyMember_Message",
                "玩家已经在队伍中。");
            return false;
        }

        if (!ServerGroups.TryGetValue(groupKey.ToString(), out group))
        {
            message = GroupOperationMessage.Create(
                "TeamMissing_Message",
                "目标队伍不存在。");
            return false;
        }

        message = GroupOperationMessage.Create(string.Empty, string.Empty);
        return true;
    }

    private PendingGroupOperation AddPendingOperation(
        PendingGroupOperationKind kind,
        Guid initiator,
        Guid responder,
        Guid joiningPlayer,
        Guid groupKey)
    {
        var operation = new PendingGroupOperation(
            Guid.NewGuid(),
            kind,
            initiator,
            responder,
            joiningPlayer,
            groupKey,
            Time.RealTime + _groupOperationLifetime);
        _pendingGroupOperations.Add(operation.OperationId, operation);
        return operation;
    }

    private bool HasPendingOperationForPlayer(Guid playerGuid)
    {
        CleanupExpiredGroupOperations();
        return _pendingGroupOperations.Values.Any(operation =>
            operation.JoiningPlayer == playerGuid);
    }

    private void CleanupExpiredGroupOperations()
    {
        foreach (var operationId in _pendingGroupOperations
                     .Where(pair => pair.Value.ExpiresAt <= Time.RealTime)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _pendingGroupOperations.Remove(operationId);
        }
    }

    private void RemovePendingOperationsForPlayer(Guid playerGuid)
    {
        foreach (var operationId in _pendingGroupOperations
                     .Where(pair =>
                         pair.Value.Initiator == playerGuid ||
                         pair.Value.Responder == playerGuid ||
                         pair.Value.JoiningPlayer == playerGuid)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _pendingGroupOperations.Remove(operationId);
        }
    }

    private void RemovePendingOperationsForGroup(Guid groupKey)
    {
        foreach (var operationId in _pendingGroupOperations
                     .Where(pair => pair.Value.GroupKey == groupKey)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _pendingGroupOperations.Remove(operationId);
        }
    }
}
