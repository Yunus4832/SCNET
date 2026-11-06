namespace Game.Network.Transport;

public static class SnapshotPackageCoalescer
{
    public static bool TryCoalesce(List<OutboundPackage> pendingPackages, OutboundPackage newer)
    {
        var index = FindReplacement(pendingPackages, newer);
        if (index < 0) { return false; }

        pendingPackages[index].Fanout?.Advance(1, 0);
        pendingPackages[index] = newer;
        return true;
    }

    internal static int FindReplacement(IReadOnlyList<OutboundPackage> pendingPackages, OutboundPackage newer)
    {
        if (!TryGetKey(newer.Package, newer.Audience, out var key)) { return -1; }

        for (var index = pendingPackages.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(pendingPackages[index].Runtime, newer.Runtime) &&
                TryGetKey(pendingPackages[index].Package, pendingPackages[index].Audience, out var pendingKey) &&
                pendingKey.Type == key.Type &&
                pendingKey.EntityId == key.EntityId &&
                pendingKey.Audience.HasSameRecipients(key.Audience))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryGetKey(IProtocolMessage package, PackageAudience audience, out SnapshotKey key)
    {
        switch (package)
        {
            case CharacterStatePackage playerPackage:
                key = new SnapshotKey(
                    typeof(ComponentCharacterPackage),
                    playerPackage.ControllerClientId,
                    audience
                );
                return true;
            case OnlineCharacterStatePackage:
                key = new SnapshotKey(typeof(OnlineCharacterStatePackage), 0, audience);
                return true;
            default:
                key = default;
                return false;
        }
    }

    private readonly record struct SnapshotKey(Type Type, int EntityId, PackageAudience Audience);
}
