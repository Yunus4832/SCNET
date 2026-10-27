using Game.Network;
using Game.Protocol.Identity;

namespace TerritoryStoneMod;

public static class TerritoryMessages
{
    public const string Snapshot = "snapshot";

    public const string Update = "update";

    public const string Remove = "remove";

    public const string Settings = "settings";

    public const string AccessRequest = "access-request";

    public const string AccessRequestRemoved = "access-request-removed";

    public const string SettingsResult = "settings-result";

    public static bool CanChangeSettings(Client? sender, PlayerAddress owner) =>
        sender is { Subject: { Ban: null } } &&
        owner != default &&
        sender.PlayerAddress == owner &&
        ReferenceEquals(CommonLib.Net.Clients.GetValueOrDefault(sender.ID), sender);

    public static TerritorySnapshot Capture(Territory territory) => new(
        territory.Owner,
        territory.StonePoint,
        territory.Settings,
        territory.Grants.Select(pair => new TerritoryGrantSnapshot(pair.Key, pair.Value.Name, pair.Value.Permissions))
            .ToArray()
    );

    public static Territory Restore(TerritorySnapshot snapshot)
    {
        if (snapshot.Grants.Length > 128) { throw new InvalidDataException("Too many territory grants."); }

        var territory = new Territory(snapshot.Owner, snapshot.StonePoint, snapshot.Settings);
        var subjects = new HashSet<PlayerAddress>();
        foreach (var grant in snapshot.Grants)
        {
            if (!subjects.Add(grant.Subject)) { throw new InvalidDataException("Duplicate territory grant."); }

            territory.SetGrant(grant.Subject, grant.Name, grant.Permissions);
        }

        return territory;
    }

    public static Territory[] Restore(TerritoryWorldSnapshot snapshot)
    {
        if (snapshot.Territories.Length > 1024) { throw new InvalidDataException("Too many territories."); }

        return snapshot.Territories.Select(Restore).ToArray();
    }
}
