using EntitySystem.Core;

using Game.Network;
using Game.Protocol.Identity;

namespace Game.Admission;

/// <summary>World admission rules shared by trusted local access and remote transports.</summary>
internal static class PlayerAdmission
{
    internal static string? Rejection(Project project, NetNode node, PlayerAddress address, Guid token)
    {
        if (token == Guid.Empty) { return "连接关联标识无效"; }

        if (node.Clients.Values.Any(client => client.PlayerAddress == address) ||
            node.PendingPlayer?.PlayerAddress == address)
        {
            return "该用户已经在线";
        }

        if (node.Clients.Values.Any(client => client.TokenId == token) || node.PendingPlayer?.TokenId == token)
        {
            return "连接关联标识已被占用";
        }

        if (project.FindSubsystem<SubsystemPlayers>(true)!.Store.Find(address)?.Ban is not null)
        {
            return "你被禁止加入该服务器";
        }

        var reserved = node.PendingPlayer?.PlayerAddress is not null ? 1 : 0;
        if (node.IsServer &&
            node.ClientCount + reserved >=
            project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.MaxOnlinePlayerCount)
        {
            return "在线人数达到最大，拒绝加入";
        }

        return null;
    }

    internal static bool TryCreateSession(
        Project project,
        NetNode node,
        PlayerAddress address,
        Guid token,
        Func<Guid, Client> create,
        out Client? session,
        out string? error
    )
    {
        session = null;
        error = Rejection(project, node, address, token);
        if (error is not null) { return false; }

        var players = project.FindSubsystem<SubsystemPlayers>(true)!.Store;
        try
        {
            var created = create(players.Find(address)?.CharacterId ?? Guid.Empty);
            if (created.PlayerAddress != address || created.TokenId != token)
            {
                throw new InvalidOperationException(
                    "The admitted session does not match its verified subject and token."
                );
            }

            players.GetOrCreate(address);
            created.State = ClientState.Connected;
            session = created;
            return true;
        }
        catch (Exception exception)
        {
            Log.Error(exception);
            error = "玩家会话创建失败，请稍后重试";
            return false;
        }
    }
}
