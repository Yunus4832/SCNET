using Game.Network.Enums;

namespace Game.Network.Packages.Handlers;

public sealed class ClientPackageHandler : PackageHandlerBase<ClientPackage>
{
    internal static Client ResolveServerClient(Client? current, Client incoming, Client? sender)
    {
        if (current is not null && current.GUID == incoming.GUID &&
            ReferenceEquals(current.Peer, sender?.Peer))
        {
            current.ID = incoming.ID;
            current.TokenId = incoming.TokenId;
            current.State = incoming.State;
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

        switch (package.PackageEventType)
        {
            case ClientPackage.EventType.Add:
                if (GameManager.Project is null)
                {
                    return;
                }

                var project = GameManager.Project;
                netNode.AddClient(new Client(
                    context.Sender?.Peer,
                    package.Client!.ID,
                    package.Client.TokenId,
                    package.Client.GUID,
                    project)
                );
                break;
            case ClientPackage.EventType.Remove:
                if (netNode.Clients.ContainsKey(package.Client!.ID))
                {
                    var client = netNode.Clients[package.Client.ID];
                    client.State = ClientState.NotConnected;
                    netNode.OnClientStateChanged?.Invoke(client);
                    netNode.Clients.Remove(package.Client.ID);
                }

                break;
            case ClientPackage.EventType.SyncList:
                foreach (var c in package.List)
                {
                    var client = c;
                    if (c.ID == 0)
                    {
                        client = ResolveServerClient(netNode.Server, c, context.Sender);
                        client.Peer?.Tag = client;
                        netNode.Server = client;
                    }
                    else
                    {
                        if (c.TokenId == CommonLib.Net.TokenId)
                        {
                            netNode.Self = c;
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
            case ClientPackage.EventType.StateChange:
                if (netNode.Clients.TryGetValue(package.Client!.ID, out var nodeClient))
                {
                    if (nodeClient.State != package.Client.State)
                    {
                        nodeClient.State = package.Client.State;
                        netNode.OnClientStateChanged?.Invoke(nodeClient);
                    }
                }

                break;
        }
    }
}
