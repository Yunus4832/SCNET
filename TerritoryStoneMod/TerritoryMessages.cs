using Engine.Core;

using Game.Network;
using Game.Network.Serialization;

namespace TerritoryStoneMod;

public static class TerritoryMessages
{
    public const string Snapshot = "snapshot";
    public const string Update = "update";
    public const string Remove = "remove";
    public const string Settings = "settings";
    public const string SettingsResult = "settings-result";

    public static bool CanChangeSettings(Client? sender, Guid owner) =>
        sender != null && owner != Guid.Empty && sender.GUID == owner;

    public static void Write(PackageStreamWriter writer, Territory territory)
    {
        writer.Write(territory.Owner);
        writer.Write(territory.StonePoint);
        writer.Write(territory.ApplyToTeam);
        writer.Write(territory.ShowBoundary);
        writer.Write(territory.RestrictEntry);
    }

    public static Territory Read(PackageStreamReader reader)
    {
        var owner = reader.ReadGuid();
        var point = reader.ReadPoint3();
        if (owner == Guid.Empty || point.Y is < 0 or > 255)
        {
            throw new InvalidDataException("Invalid territory identity or stone height.");
        }

        return new Territory(owner, point)
        {
            ApplyToTeam = reader.ReadBoolean(),
            ShowBoundary = reader.ReadBoolean(),
            RestrictEntry = reader.ReadBoolean()
        };
    }

    public static void WriteSnapshot(PackageStreamWriter writer, IReadOnlyCollection<Territory> territories)
    {
        writer.Write(territories.Count);
        foreach (var territory in territories)
        {
            Write(writer, territory);
        }
    }

    public static Territory[] ReadSnapshot(PackageStreamReader reader)
    {
        var count = reader.ReadInt32();
        if (count is < 0 or > 1024)
        {
            throw new InvalidDataException("Invalid territory snapshot count.");
        }

        var territories = new Territory[count];
        for (var index = 0; index < count; index++)
        {
            territories[index] = Read(reader);
        }

        return territories;
    }
}
