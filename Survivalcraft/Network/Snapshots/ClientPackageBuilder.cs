namespace Game.Network.Snapshots;

public static class ClientPackageBuilder
{
    public static ClientIdentity Capture(Client client) => new(
        client.ID,
        client.TokenId,
        client.CharacterId,
        client.PlayerAddress,
        client.State
    );

    public static ClientListPackage Snapshot(IEnumerable<Client> clients) =>
        new() { List = clients.Select(Capture).ToArray() };

    public static Client Restore(ClientIdentity client) =>
        new(null, client.ID, client.TokenId, client.CharacterId, null)
        {
            PlayerAddress = client.PlayerAddress,
            State = client.State
        };
}
