namespace Game.Network.Snapshots;

public static class ClientPackageBuilder
{
    public static ClientIdentity Capture(Client client) => new(client.ID, client.TokenId, client.GUID, client.State);
    public static ClientPackage Snapshot(IEnumerable<Client> clients) => new(clients.Select(Capture).ToArray());
    public static Client Restore(ClientIdentity client) => new(null, client.ID, client.TokenId, client.GUID, null)
    {
        State = client.State
    };
}
