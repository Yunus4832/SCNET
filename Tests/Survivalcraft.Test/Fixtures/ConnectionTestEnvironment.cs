using Game.Network;

namespace Survivalcraft.Test.Fixtures;

internal static class ConnectionTestEnvironment
{
    internal static void SetRoster(NetNode node, IEnumerable<Client> connections)
    {
        var requested = connections.ToArray();
        foreach (var previous in node.Clients.Values.ToArray())
        {
            if (!requested.Any(connection => ReferenceEquals(connection, previous)))
            {
                node.UnregisterClient(previous);
            }
        }

        foreach (var connection in requested)
        {
            if (!node.Clients.TryGetValue(connection.ID, out var current) || !ReferenceEquals(current, connection))
            {
                node.AddClient(connection);
            }
        }
    }
}
