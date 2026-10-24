namespace Game.Network.Transport;

public static class SnapshotPackageCoalescer
{
    public static bool TryCoalesce(List<OutboundPackage> pendingPackages, OutboundPackage newer)
    {
        if (!TryGetKey(newer.Package, newer.Audience, out var key))
        {
            return false;
        }

        for (var i = pendingPackages.Count - 1; i >= 0; i--)
        {
            if (!TryGetKey(pendingPackages[i].Package, pendingPackages[i].Audience, out var pendingKey) ||
                pendingKey.Type != key.Type ||
                pendingKey.EntityId != key.EntityId ||
                !pendingKey.Audience.HasSameRecipients(key.Audience))
            {
                continue;
            }

            pendingPackages[i].Fanout?.Advance(1, 0);
            pendingPackages[i] = newer;
            return true;
        }

        return false;
    }

    private static bool TryGetKey(IProtocolMessage package, PackageAudience audience, out SnapshotKey key)
    {
        switch (package)
        {
            case CharacterStatePackage playerPackage:
                key = new SnapshotKey(
                    typeof(ComponentCharacterPackage),
                    playerPackage.ControllerSessionId,
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
