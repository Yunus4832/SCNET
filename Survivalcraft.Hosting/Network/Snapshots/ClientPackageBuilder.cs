namespace Game.Network.Snapshots;

public static class ClientPackageBuilder
{
    public static ClientIdentity Capture(Client client) => new(
        client.ID,
        client.TokenId,
        client.CharacterId,
        client.PlayerAddress,
        client.State,
        client.ConnectionEpoch,
        client.ConnectionGeneration
    );

    public static ClientListPackage Snapshot(IEnumerable<Client> clients) =>
        new() { List = clients.Select(Capture).ToArray() };

    public static ClientStateChangedPackage State(WorldConnection client) => new()
    {
        ClientId = client.ID, State = client.State,
        ConnectionEpoch = client.ConnectionEpoch, ConnectionGeneration = client.ConnectionGeneration
    };
}
