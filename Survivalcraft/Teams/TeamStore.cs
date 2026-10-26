using System.Collections.ObjectModel;

using EntitySystem.TemplatesDatabase;

using Game.Players;
using Game.Protocol.Identity;

namespace Game.Teams;

/// <summary>Player membership and consent transactions. No character, session, packet or UI dependencies.</summary>
public sealed class TeamStore
{
    private readonly WorldPlayerStore _players;

    private readonly bool _isAuthoritative;

    private readonly Func<double> _clock;

    private readonly Dictionary<Guid, Team> _teams = [];

    private readonly Dictionary<PlayerAddress, Guid> _membership = [];

    private readonly Dictionary<Guid, TeamOperation> _operations = [];

    private readonly Dictionary<PlayerAddress, double> _lastOperations = [];

    public IReadOnlyDictionary<Guid, Team> Teams { get; }

    public TeamStore(WorldPlayerStore players, bool isAuthoritative = true, Func<double>? clock = null)
    {
        _players = players ?? throw new ArgumentNullException(nameof(players));
        _isAuthoritative = isAuthoritative;
        _clock = clock ?? (() => Time.RealTime);
        Teams = new ReadOnlyDictionary<Guid, Team>(_teams);
    }

    public Team? Find(Guid id) => _teams.GetValueOrDefault(id);

    public Team? FindForPlayer(PlayerAddress player)
    {
        return _membership.TryGetValue(player, out var id) ? _teams[id] : null;
    }

    public bool TryBeginOperation(WorldPlayer actor, out TeamMessage? error)
    {
        RequireActor(actor);
        ExpireOperations();
        var now = _clock();
        if (_lastOperations.TryGetValue(actor.Address, out var last) && now - last < 1.0)
        {
            error = TeamMessage.Create("TeamRateLimited_Message", "操作过于频繁，请稍后重试。");
            return false;
        }

        _lastOperations[actor.Address] = now;
        error = null;
        return true;
    }

    public bool TryCreate(WorldPlayer actor, string requestedName, out TeamMessage message)
    {
        RequireActor(actor);
        var name = requestedName.Trim();
        if (name.Length is < 1 or > 64 || name.Any(char.IsControl))
        {
            message = TeamMessage.Create("TeamNameInvalid_Message", "队伍名称必须为 1 到 64 个有效字符。");
            return false;
        }

        if (FindForPlayer(actor.Address) is not null)
        {
            message = TeamMessage.Create("TeamAlreadyMember_Message", "你已经在队伍中。");
            return false;
        }

        var team = new Team(Guid.NewGuid(), name, actor.Address, [actor.Address]);
        _teams.Add(team.Id, team);
        _membership.Add(actor.Address, team.Id);
        CancelOperationsFor(actor.Address);
        message = TeamMessage.Create("TeamCreated_Message", "已创建队伍“{0}”。", name);
        return true;
    }

    public bool TryLeave(WorldPlayer actor, out TeamMessage message)
    {
        RequireActor(actor);
        var team = FindForPlayer(actor.Address);
        if (team is null)
        {
            message = TeamMessage.Create("TeamNotMember_Message", "你当前不在队伍中。");
            return false;
        }

        CancelOperationsFor(actor.Address);
        if (team.Leader == actor.Address)
        {
            _teams.Remove(team.Id);
            foreach (var member in team.Members) { _membership.Remove(member); }

            foreach (var operation in _operations.Values.Where(item => item.TeamId == team.Id).ToArray())
            {
                _operations.Remove(operation.Id);
            }

            message = TeamMessage.Create("TeamDisbanded_Message", "队长退出，队伍已解散。");
            return true;
        }

        team.RemoveMember(actor.Address);
        _membership.Remove(actor.Address);
        message = TeamMessage.Create("TeamLeft_Message", "已退出队伍“{0}”。", team.Name);
        return true;
    }

    public bool TryRequestJoin(WorldPlayer actor, Guid teamId, out TeamOperation? operation, out TeamMessage message)
    {
        RequireActor(actor);
        operation = null;
        if (!CanJoin(actor.Address, teamId, out var team, out message) || !CanRequest(actor.Address, out message))
        {
            return false;
        }

        operation = AddOperation(TeamOperationKind.JoinRequest, actor.Address, team!.Leader, actor.Address, teamId);
        message = TeamMessage.Create("TeamJoinSent_Message", "已向队伍“{0}”发送加入申请。", team.Name);
        return true;
    }

    public bool TryInvite(
        WorldPlayer actor,
        Guid teamId,
        PlayerAddress target,
        out TeamOperation? operation,
        out TeamMessage message
    )
    {
        RequireActor(actor);
        operation = null;
        if (Find(teamId) is not { } team || !team.Members.Contains(actor.Address))
        {
            message = TeamMessage.Create("TeamNotMember_Message", "你不是该队伍的成员。");
            return false;
        }

        if (_players.Find(target) is not { Ban: null })
        {
            message = TeamMessage.Create("TeamPlayerUnavailable_Message", "该玩家不存在或已被封禁。");
            return false;
        }

        if (!CanJoin(target, teamId, out _, out message) || !CanRequest(target, out message)) { return false; }

        operation = AddOperation(TeamOperationKind.Invitation, actor.Address, target, target, teamId);
        // Display names are supplied by presentation; the domain uses the stable address.
        message = TeamMessage.Create("TeamInvitationSent_Message", "已向 {0} 发送队伍邀请。", target.Value);
        return true;
    }

    public TeamOperation? FindOperation(WorldPlayer actor, Guid id)
    {
        RequireActor(actor);
        ExpireOperations();
        return _operations.TryGetValue(id, out var operation) && operation.Responder == actor.Address
            ? operation
            : null;
    }

    public bool TryRespond(
        WorldPlayer actor,
        Guid operationId,
        bool accepted,
        out TeamOperation? operation,
        out TeamMessage message
    )
    {
        RequireActor(actor);
        ExpireOperations();
        if (!_operations.TryGetValue(operationId, out operation) || operation.Responder != actor.Address)
        {
            operation = null;
            message = TeamMessage.Create("TeamRequestExpired_Message", "该队伍请求不存在或已经失效。");
            return false;
        }

        _operations.Remove(operationId);
        if (!accepted)
        {
            message = TeamMessage.Create("TeamRequestRejected_Message", "已拒绝队伍请求。");
            return true;
        }

        if (Find(operation.TeamId) is not { } team ||
            _players.Find(operation.JoiningPlayer) is not { Ban: null } ||
            _players.Find(operation.Initiator) is not { Ban: null } ||
            (operation.Kind == TeamOperationKind.JoinRequest
                ? team.Leader != actor.Address
                : !team.Members.Contains(operation.Initiator)))
        {
            message = TeamMessage.Create("TeamStateInvalid_Message", "队伍状态无效，无法处理加入申请。");
            return false;
        }

        if (!CanJoin(operation.JoiningPlayer, team.Id, out _, out message)) { return false; }

        team.AddMember(operation.JoiningPlayer);
        _membership.Add(operation.JoiningPlayer, team.Id);
        CancelOperationsFor(operation.JoiningPlayer);
        message = TeamMessage.Create("TeamJoined_Message", "已加入队伍“{0}”。", team.Name);
        return true;
    }

    public void CancelOperationsFor(PlayerAddress player)
    {
        foreach (var item in _operations.Values.Where(item =>
                     item.Initiator == player || item.Responder == player || item.JoiningPlayer == player
                 ).ToArray())
        {
            _operations.Remove(item.Id);
        }
    }

    private bool CanJoin(PlayerAddress player, Guid id, out Team? team, out TeamMessage message)
    {
        team = Find(id);
        if (FindForPlayer(player) is not null)
        {
            message = TeamMessage.Create("TeamPlayerAlreadyMember_Message", "玩家已经在队伍中。");
            return false;
        }

        if (team is null)
        {
            message = TeamMessage.Create("TeamMissing_Message", "目标队伍不存在。");
            return false;
        }

        message = TeamMessage.Create(string.Empty, string.Empty);
        return true;
    }

    private bool CanRequest(PlayerAddress player, out TeamMessage message)
    {
        ExpireOperations();
        if (_operations.Values.Any(item => item.JoiningPlayer == player))
        {
            message = TeamMessage.Create("TeamRequestPending_Message", "你已经有一个待处理的队伍请求。");
            return false;
        }

        message = TeamMessage.Create(string.Empty, string.Empty);
        return true;
    }

    private TeamOperation AddOperation(
        TeamOperationKind kind,
        PlayerAddress initiator,
        PlayerAddress responder,
        PlayerAddress joining,
        Guid team
    )
    {
        var operation = new TeamOperation(Guid.NewGuid(), kind, initiator, responder, joining, team, _clock() + 60.0);
        _operations.Add(operation.Id, operation);
        return operation;
    }

    private void ExpireOperations()
    {
        foreach (var item in _operations.Values.Where(item => item.ExpiresAt <= _clock()).ToArray())
        {
            _operations.Remove(item.Id);
        }
    }

    private void RequireActor(WorldPlayer actor)
    {
        if (!_isAuthoritative || !ReferenceEquals(_players.Find(actor.Address), actor) || actor.Ban is not null)
        {
            throw new InvalidOperationException("Team mutations require an admitted player and server authority.");
        }
    }

    public ValuesDictionary Save()
    {
        var result = new ValuesDictionary();
        foreach (var team in _teams.Values.OrderBy(item => item.Id))
        {
            var record = new ValuesDictionary();
            record.SetValue("Name", team.Name);
            record.SetValue("Leader", team.Leader.Value);
            var members = new ValuesDictionary();
            var index = 0;
            foreach (var member in team.Members.OrderBy(item => item.Value, StringComparer.Ordinal))
            {
                members.SetValue((index++).ToString(), member.Value);
            }

            record.SetValue("Members", members);
            result.SetValue(team.Id.ToString("N"), record);
        }

        return result;
    }

    public void Load(ValuesDictionary values)
    {
        var teams = new List<Team>();
        foreach (var entry in values)
        {
            if (!Guid.TryParseExact(entry.Key, "N", out var id) || entry.Value is not ValuesDictionary record)
            {
                throw new InvalidDataException("Invalid team record.");
            }

            teams.Add(
                new Team(
                    id,
                    record.GetValue<string>("Name"),
                    new PlayerAddress(record.GetValue<string>("Leader")),
                    record.GetValue<ValuesDictionary>("Members").Values.Cast<string>()
                        .Select(value => new PlayerAddress(value))
                )
            );
        }

        Replace(teams);
    }

    internal void Replace(IEnumerable<Team> teams)
    {
        var replacement = new Dictionary<Guid, Team>();
        var membership = new Dictionary<PlayerAddress, Guid>();
        foreach (var team in teams)
        {
            if (team.Id == Guid.Empty ||
                team.Name.Length is < 1 or > 64 ||
                team.Name.Any(char.IsControl) ||
                !team.Members.Contains(team.Leader) ||
                !replacement.TryAdd(team.Id, new Team(team.Id, team.Name, team.Leader, team.Members)))
            {
                throw new InvalidDataException("Invalid team identity, name or leader.");
            }

            foreach (var member in team.Members)
            {
                if (!membership.TryAdd(member, team.Id) || _isAuthoritative && _players.Find(member) is null)
                {
                    throw new InvalidDataException("Team members must be known players belonging to exactly one team.");
                }
            }
        }

        _teams.Clear();
        _membership.Clear();
        foreach (var pair in replacement) { _teams.Add(pair.Key, pair.Value); }

        foreach (var pair in membership) { _membership.Add(pair.Key, pair.Value); }

        _operations.Clear();
        _lastOperations.Clear();
    }
}
