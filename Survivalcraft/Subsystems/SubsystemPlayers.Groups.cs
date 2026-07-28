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

    private const double _groupOperationLifetime = 60.0;

    private const double _groupOperationMinimumInterval = 1.0;

    private readonly Dictionary<Guid, double> _lastGroupOperationTimes = [];

    private readonly Dictionary<Guid, PendingGroupOperation> _pendingGroupOperations = [];

    public bool TryStartGroupOperation(PlayerData actor, out string error)
    {
        CleanupExpiredGroupOperations();
        var now = Time.RealTime;
        if (_lastGroupOperationTimes.TryGetValue(actor.PlayerGUID, out var lastTime) &&
            now - lastTime < _groupOperationMinimumInterval)
        {
            error = "操作过于频繁，请稍后重试。";
            return false;
        }

        _lastGroupOperationTimes[actor.PlayerGUID] = now;
        error = string.Empty;
        return true;
    }

    public bool TryCreateGroup(PlayerData actor, string requestedName, out string message)
    {
        var name = requestedName.Trim();
        if (name.Length is < 1 or > 64 || name.Any(char.IsControl))
        {
            message = "队伍名称必须为 1 到 64 个有效字符。";
            return false;
        }

        if (GetPlayerGroupKey(actor.PlayerGUID).Length > 0 || actor.GroupKey.Length > 0)
        {
            message = "你已经在队伍中。";
            return false;
        }

        var key = actor.PlayerGUID.ToString();
        if (ServerGroups.ContainsKey(key))
        {
            message = "无法创建重复的队伍。";
            return false;
        }

        var group = new Group { Name = name };
        group.Members.Add(actor.PlayerGUID);
        ServerGroups.Add(key, group);
        actor.GroupKey = key;
        message = $"已创建队伍“{name}”。";
        return true;
    }

    public bool TryLeaveGroup(PlayerData actor, out string message)
    {
        var groupKey = GetPlayerGroupKey(actor.PlayerGUID);
        if (groupKey.Length == 0 ||
            !ServerGroups.TryGetValue(groupKey, out var group))
        {
            actor.GroupKey = string.Empty;
            message = "你当前不在队伍中。";
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
            message = "队长退出，队伍已解散。";
            return true;
        }

        group.Members.Remove(actor.PlayerGUID);
        actor.GroupKey = string.Empty;
        RemovePendingOperationsForPlayer(actor.PlayerGUID);
        message = $"已退出队伍“{group.Name}”。";
        return true;
    }

    public bool TryCreateJoinRequest(
        PlayerData requester,
        Guid groupKey,
        out PendingGroupOperation? operation,
        out PlayerData? responder,
        out string message)
    {
        operation = null;
        responder = null;
        if (!TryValidateCanJoin(requester, groupKey, out var group, out message))
        {
            return false;
        }

        if (!group!.Members.Contains(groupKey))
        {
            message = "队伍状态无效，无法处理加入申请。";
            return false;
        }

        if (HasPendingOperationForPlayer(requester.PlayerGUID))
        {
            message = "你已经有一个待处理的队伍请求。";
            return false;
        }

        responder = FindPlayerData(player => player.PlayerGUID == groupKey);
        if (responder is null || responder.Client is null && !responder.IsMainPlayer)
        {
            message = "队长当前不在线，无法处理加入申请。";
            return false;
        }

        operation = AddPendingOperation(
            PendingGroupOperationKind.JoinRequest,
            requester.PlayerGUID,
            responder.PlayerGUID,
            requester.PlayerGUID,
            groupKey);
        message = $"已向队伍“{group!.Name}”发送加入申请。";
        return true;
    }

    public bool TryCreateInvitation(
        PlayerData inviter,
        Guid groupKey,
        Guid targetPlayer,
        out PendingGroupOperation? operation,
        out PlayerData? responder,
        out string message)
    {
        operation = null;
        responder = null;
        if (!ServerGroups.TryGetValue(groupKey.ToString(), out var group) ||
            !group.Members.Contains(inviter.PlayerGUID))
        {
            message = "你不是该队伍的成员。";
            return false;
        }

        responder = FindPlayerData(player => player.PlayerGUID == targetPlayer);
        if (responder is null || responder.Client is null && !responder.IsMainPlayer)
        {
            message = "目标玩家当前不在线。";
            return false;
        }

        if (GetPlayerGroupKey(targetPlayer).Length > 0 || responder.GroupKey.Length > 0)
        {
            message = "目标玩家已经在队伍中。";
            return false;
        }

        if (HasPendingOperationForPlayer(targetPlayer))
        {
            message = "目标玩家已经有一个待处理的队伍请求。";
            return false;
        }

        operation = AddPendingOperation(
            PendingGroupOperationKind.Invitation,
            inviter.PlayerGUID,
            responder.PlayerGUID,
            responder.PlayerGUID,
            groupKey);
        message = $"已向 {responder.Name} 发送队伍邀请。";
        return true;
    }

    public bool TryRespondToGroupOperation(
        PlayerData responder,
        Guid operationId,
        bool accepted,
        out PendingGroupOperation? operation,
        out string message)
    {
        CleanupExpiredGroupOperations();
        if (!_pendingGroupOperations.TryGetValue(operationId, out operation) ||
            operation.Responder != responder.PlayerGUID)
        {
            operation = null;
            message = "该队伍请求不存在或已经失效。";
            return false;
        }

        _pendingGroupOperations.Remove(operationId);
        if (!accepted)
        {
            message = "已拒绝队伍请求。";
            return true;
        }

        var joiningPlayerGuid = operation.JoiningPlayer;
        var joiningPlayer = FindPlayerData(player => player.PlayerGUID == joiningPlayerGuid);
        if (joiningPlayer is not null)
        {
            return TryJoinGroup(joiningPlayer, operation.GroupKey, out message);
        }

        message = "申请加入的玩家已经离线。";
        return false;
    }

    private bool TryJoinGroup(PlayerData player, Guid groupKey, out string message)
    {
        if (!TryValidateCanJoin(player, groupKey, out var group, out message))
        {
            return false;
        }

        group!.Members.Add(player.PlayerGUID);
        player.GroupKey = groupKey.ToString();
        RemovePendingOperationsForPlayer(player.PlayerGUID);
        message = $"已加入队伍“{group.Name}”。";
        return true;
    }

    private bool TryValidateCanJoin(
        PlayerData player,
        Guid groupKey,
        out Group? group,
        out string message)
    {
        group = null;
        var currentGroup = GetPlayerGroupKey(player.PlayerGUID);
        if (currentGroup.Length > 0 || player.GroupKey.Length > 0)
        {
            message = "玩家已经在队伍中。";
            return false;
        }

        if (!ServerGroups.TryGetValue(groupKey.ToString(), out group))
        {
            message = "目标队伍不存在。";
            return false;
        }

        message = string.Empty;
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
