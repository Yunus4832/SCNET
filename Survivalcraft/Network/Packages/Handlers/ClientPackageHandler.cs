using Game.Network.Enums;

namespace Game.Network.Packages.Handlers;

public sealed class ClientPackageHandler : PackageHandlerBase<ClientPackage>
{
    public override void Handle(ClientPackage package, NetNode? netNode, bool isServer)
    {
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
                    package.From?.Peer,
                    package.Client!.ID,
                    package.Client.TokenId,
                    package.Client.GUID, project,
                    package.Client.CommunityAccountId,
                    package.Client.Nickname)
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
                    if (c.ID == 0)
                    {
                        c.Peer = package.From?.Peer;
                        c.Peer?.Tag = c;
                        netNode.Server = package.From;
                    }
                    else
                    {
                        if (c.TokenId == CommonLib.Net.TokenId)
                        {
                            netNode.Self = c;
                        }
                    }

                    netNode.AddClient(c);
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
                    package.From = nodeClient;
                    if (package.From.State != package.Client.State)
                    {
                        package.From.State = package.Client.State;
                        netNode.OnClientStateChanged?.Invoke(package.From);
                    }
                }

                break;
        }
    }
}
