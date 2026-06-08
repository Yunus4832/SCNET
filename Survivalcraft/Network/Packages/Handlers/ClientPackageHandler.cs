using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ClientPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        switch (PackageEventType)
        {
            case EventType.Add:
                if (GameManager.Project is null)
                {
                    return;
                }

                var project = GameManager.Project;
                netNode.AddClient(new Client(From?.Peer, Client!.ID, Client.TokenId, Client.GUID, project,
                    Client.CommunityAccountId, Client.Nickname));
                break;
            case EventType.Remove:
                if (netNode.Clients.ContainsKey(Client!.ID))
                {
                    var client = netNode.Clients[Client.ID];
                    client.State = ClientState.NotConnected;
                    netNode.OnClientStateChanged?.Invoke(client);
                    netNode.Clients.Remove(Client.ID);
                }

                break;
            case EventType.SyncList:
                foreach (var c in List)
                {
                    if (c.ID == 0)
                    {
                        c.Peer = From?.Peer;
                        c.Peer?.Tag = c;
                        netNode.Server = From;
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
            case EventType.StateChange:
                if (netNode.Clients.TryGetValue(Client!.ID, out var nodeClient))
                {
                    From = nodeClient;
                    if (From.State != Client.State)
                    {
                        From.State = Client.State;
                        netNode.OnClientStateChanged?.Invoke(From);
                    }
                }

                break;
        }
    }
}

public sealed class ClientPackageHandler : PackageHandlerBase<ClientPackage>
{
    public override void Handle(ClientPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ClientPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
