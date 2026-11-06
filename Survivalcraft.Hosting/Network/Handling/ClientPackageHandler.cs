namespace Game.Network.Handling;

public sealed class ClientPackageHandler : PackageHandlerBase<ClientPackage>
{
    internal static bool InstallController(
        NetNode node,
        CharacterControllerSnapshot snapshot,
        Client? initialSelf = null
    )
    {
        var self = initialSelf ?? (node.Self?.ID == snapshot.ConnectionId ? node.Self : null);
        if (self is { ConnectionGeneration: 0 } && initialSelf is null) { return false; }

        if (self is { ConnectionGeneration: > 0 } &&
            (self.ConnectionGeneration != snapshot.ConnectionGeneration ||
             self.ConnectionEpoch != snapshot.ConnectionEpoch)) { return false; }

        return node.Connections.TryInstallController(snapshot, out _, self);
    }

    private static CharacterControllerSnapshot Controller(ClientIdentity identity) => new(
        identity.ID,
        identity.CharacterId,
        identity.PlayerAddress,
        identity.State,
        identity.ConnectionEpoch,
        identity.ConnectionGeneration
    );

    public override void Handle(ClientPackage package, PackageReceiveContext context)
    {
        var node = context.Node;
        if (node is null || !ReferenceEquals(context.Runtime, node.Runtime) || context.Runtime.IsClosed) { return; }

        if (context.IsServer)
        {
            if (package is not ClientStateChangedPackage change ||
                !node.Connections.MatchesLifetime(
                    change.ClientId,
                    change.ConnectionEpoch,
                    change.ConnectionGeneration,
                    out var target
                ) ||
                !ReferenceEquals(context.Sender, target)) { return; }
        }
        else if (context.Sender is null || node.Server is not null && !ReferenceEquals(context.Sender, node.Server))
        {
            if (package is ClientListPackage)
            {
                throw new InvalidDataException("A roster cannot replace its authority transport.");
            }

            return;
        }

        switch (package)
        {
            case ClientAddedPackage added:
                if (added.Client is not { ID: > 0 } addedIdentity)
                {
                    throw new InvalidDataException("Invalid player roster row.");
                }

                if (InstallController(node, Controller(addedIdentity)) &&
                    node.Connections.MatchesLifetime(
                        addedIdentity.ID,
                        addedIdentity.ConnectionEpoch,
                        addedIdentity.ConnectionGeneration,
                        out var addedRecord
                    ))
                {
                    addedRecord!.State = addedIdentity.State;
                }

                break;
            case ClientListPackage list:
                if (list.List.Any(identity => identity.ID == 0) ||
                    !list.List.Any(identity => identity.TokenId == node.TokenId))
                {
                    throw new InvalidDataException(
                        "The roster must identify this player and exclude the authority transport."
                    );
                }

                node.Server = context.Sender;
                foreach (var identity in list.List)
                {
                    var isSelf = identity.TokenId == node.TokenId;
                    Client? self = null;
                    if (isSelf)
                    {
                        self = node.Self;
                        if (self is null)
                        {
                            self = new Client(null, identity.ID, identity.TokenId, identity.CharacterId)
                                { PlayerAddress = identity.PlayerAddress };
                            node.Self = self;
                        }

                        if (self.TokenId != identity.TokenId || self.ID != identity.ID)
                        {
                            throw new InvalidDataException("Self identity changed within an endpoint.");
                        }
                    }

                    if (!InstallController(node, Controller(identity), self)) { continue; }

                    if (isSelf)
                    {
                        if (!ReferenceEquals(node.Connections.Members[identity.ID], self))
                        {
                            throw new InvalidDataException("A newer self lifetime requires a new endpoint.");
                        }

                        node.AddClient(self!);
                    }
                }

                node.CurrentStage = NetNode.Stage.Connected;
                break;
            case ClientStateChangedPackage state:
                if (!node.Connections.MatchesLifetime(
                        state.ClientId,
                        state.ConnectionEpoch,
                        state.ConnectionGeneration,
                        out var changed
                    )) { return; }

                if (context.IsServer && state.State == ClientState.Playing)
                {
                    WorldConnectionResolver.FindCharacter(changed!)?.ComponentCharacter?.Entity
                        .FindComponent<ComponentIntro>()?.AcknowledgePresentation();
                }

                if (changed!.State != state.State)
                {
                    changed.State = state.State;
                    node.OnClientStateChanged?.Invoke(changed);
                }

                break;
            case ClientRemovedPackage removed:
                if (node.Connections.RemoveLifetime(
                        removed.ClientId,
                        removed.ConnectionEpoch,
                        removed.ConnectionGeneration,
                        out var retired
                    ))
                {
                    retired!.State = ClientState.NotConnected;
                    node.OnClientStateChanged?.Invoke(retired);
                    if (node.TryGetPhysicalConnection(retired, out var physical) && physical is not null)
                    {
                        node.UnregisterClient(physical);
                    }
                }

                break;
        }
    }
}
