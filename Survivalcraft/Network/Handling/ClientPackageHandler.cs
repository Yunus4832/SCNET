using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ClientPackageHandler : PackageHandlerBase<ClientPackage>
{
    internal static Client ResolveServerClient(Client? current, Client incoming, Client? sender)
    {
        if (current is not null &&
            current.PlayerAddress == incoming.PlayerAddress &&
            ReferenceEquals(current.Peer, sender?.Peer))
        {
            current.ID = incoming.ID;
            current.TokenId = incoming.TokenId;
            current.State = incoming.State;
            current.CharacterId = incoming.CharacterId;
            return current;
        }

        incoming.Peer = sender?.Peer;
        return incoming;
    }

    public override void Handle(ClientPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ClientPackage)}");
            return;
        }

        if (context.IsServer &&
            (package is not ClientStateChangedPackage change ||
             !netNode.Clients.TryGetValue(change.ClientId, out var target) ||
             !ReferenceEquals(context.Sender, target)))
        {
            return;
        }

        switch (package)
        {
            case ClientAddedPackage message1:
            {
                if (GameManager.Project is null)
                {
                    return;
                }

                var project = GameManager.Project;
                netNode.AddClient(
                    new Client(
                        context.Sender?.Peer,
                        message1.Client!.ID,
                        message1.Client.TokenId,
                        message1.Client.CharacterId,
                        project
                    ) { PlayerAddress = message1.Client.PlayerAddress }
                );
                break;
            }
            case ClientRemovedPackage message2:
            {
                if (netNode.Clients.ContainsKey(message2.ClientId))
                {
                    var client = netNode.Clients[message2.ClientId];
                    client.State = ClientState.NotConnected;
                    netNode.OnClientStateChanged?.Invoke(client);
                    netNode.Clients.Remove(message2.ClientId);
                }

                break;
            }
            case ClientListPackage message3:
            {
                foreach (var c in message3.List)
                {
                    var client = ClientPackageBuilder.Restore(c);
                    if (c.ID == 0)
                    {
                        client = ResolveServerClient(netNode.Server, client, context.Sender);
                        netNode.Server = client;
                    }
                    else
                    {
                        if (c.TokenId == CommonLib.Net.TokenId)
                        {
                            netNode.Self = client;
                        }
                    }

                    netNode.AddClient(client);
                }

                if (netNode.Self == null)
                {
                    throw new Exception("Cannot find Self In Client List");
                }

                netNode.CurrentStage = NetNode.Stage.Connected;
                break;
            }
            case ClientStateChangedPackage message4:
            {
                if (netNode.Clients.TryGetValue(message4.ClientId, out var nodeClient))
                {
                    if (nodeClient.State != message4.State)
                    {
                        nodeClient.State = message4.State;
                        netNode.OnClientStateChanged?.Invoke(nodeClient);
                    }
                }

                break;
            }
        }
    }
}
