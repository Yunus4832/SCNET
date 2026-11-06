using EntitySystem.Core;

namespace Game.Admission;

/// <summary>World admission rules shared by trusted local access and remote transports.</summary>
internal static class PlayerAdmission
{
    internal static LocalizedText? Rejection(Project project, NetNode node, PlayerAddress address, Guid token)
    {
        if (token == Guid.Empty) { return ConnectionMessages.Create("TokenInvalid", "Invalid connection token"); }

        if (node.Clients.Values.Any(client => client.PlayerAddress == address) ||
            node.PendingPlayer?.PlayerAddress == address)
        {
            return ConnectionMessages.Create("AlreadyOnline", "This player is already online");
        }

        if (node.Clients.Values.Any(client => client.TokenId == token) || node.PendingPlayer?.TokenId == token)
        {
            return ConnectionMessages.Create("TokenInUse", "Connection token is already in use");
        }

        if (project.FindSubsystem<SubsystemPlayers>(true)!.Store.Find(address)?.Ban is not null)
        {
            return ConnectionMessages.Create("Banned", "You are banned from this server");
        }

        var reserved = node.PendingPlayer?.PlayerAddress is not null ? 1 : 0;
        if (node.IsServer &&
            node.Connections.PlayerCount + reserved >=
            project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.MaxOnlinePlayerCount)
        {
            return ConnectionMessages.Create("ServerFull", "The server is full");
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
        out LocalizedText? error
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
            error = ConnectionMessages.Create(
                "SessionCreationFailed",
                "Could not create the player session. Please try again later."
            );
            return false;
        }
    }
}
