using Game.Network;
using Game.Protocol.Identity;
using Game.Protocol.Serialization;

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

    public static void Write(ProtocolWriter writer, Territory territory)
    {
        PlayerAddressCodec.Write(writer, territory.Owner);
        writer.Write(territory.StonePoint);
        WriteSettings(writer, territory.Settings);
        writer.Write(territory.Grants.Count);
        foreach (var (player, grant) in territory.Grants)
        {
            PlayerAddressCodec.Write(writer, player);
            writer.Write(grant.Name);
            writer.Write((int)grant.Permissions);
        }
    }

    public static Territory Read(ProtocolReader reader)
    {
        var owner = PlayerAddressCodec.Read(reader);
        var point = reader.ReadPoint3();
        if (owner == default || point.Y is < 0 or > 255)
        {
            throw new InvalidDataException("Invalid territory identity or stone height.");
        }

        var territory = new Territory(owner, point, ReadSettings(reader));
        var count = reader.ReadInt32();
        if (count is < 0 or > 128)
        {
            throw new InvalidDataException("Invalid territory grant count.");
        }

        for (var index = 0; index < count; index++)
        {
            var player = PlayerAddressCodec.Read(reader);
            if (territory.Grants.ContainsKey(player))
            {
                throw new InvalidDataException("Duplicate territory grant.");
            }

            territory.SetGrant(player, reader.ReadString(), (TerritoryPermission)reader.ReadInt32());
        }

        return territory;
    }

    public static void WriteSettings(ProtocolWriter writer, TerritorySettings settings)
    {
        writer.Write(settings.Size);
        writer.Write(settings.AllowEntry);
        writer.Write(settings.AllowExit);
        writer.Write(settings.AllowDigging);
        writer.Write(settings.AllowPlacing);
        writer.Write(settings.ShowBoundary);
    }

    public static TerritorySettings ReadSettings(ProtocolReader reader) => new(
        reader.ReadInt32(),
        reader.ReadBoolean(),
        reader.ReadBoolean(),
        reader.ReadBoolean(),
        reader.ReadBoolean(),
        reader.ReadBoolean()
    );

    public static void WriteSnapshot(ProtocolWriter writer, IReadOnlyCollection<Territory> territories)
    {
        writer.Write(territories.Count);
        foreach (var territory in territories)
        {
            Write(writer, territory);
        }
    }

    public static Territory[] ReadSnapshot(ProtocolReader reader)
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
