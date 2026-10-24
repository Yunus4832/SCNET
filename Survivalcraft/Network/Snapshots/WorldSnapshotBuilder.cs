using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Modding.Blocks;

namespace Game.Network.Snapshots;

public static class WorldSnapshotBuilder
{
    public static BootstrapPackage Bootstrap(
        Project project,
        Guid epoch,
        IEnumerable<Client> clients,
        byte[]? textureData,
        byte[] projectData,
        BlockIndexMapping blockMapping
    ) => new()
    {
        World = CaptureWorld(project),
        Epoch = epoch,
        ClientList = ClientPackageBuilder.Snapshot(clients),
        BlockMapping = blockMapping.Entries.Select(pair => new BlockIndexEntry(pair.Key, checked((ushort)pair.Value)))
            .ToArray(),
        TextureData = textureData ?? [],
        ProjectData = projectData
    };

    private static WorldMetadata CaptureWorld(Project project)
    {
        var info = project.FindSubsystem<SubsystemGameInfo>(true)!;
        return new WorldMetadata(
            info.WorldSettings.Name,
            info.WorldSeed,
            ProtocolEnum.Encode(info.WorldSettings.GameMode),
            info.WorldSettings.MaxOnlinePlayerCount
        );
    }

    public static InitialWorldSnapshotPackage Initial(
        Guid epoch,
        Project project,
        IEnumerable<Client> clients,
        IEnumerable<CharacterData> players,
        IEnumerable<Entity> source
    )
    {
        var entities = source as IReadOnlyList<Entity> ?? source.ToArray();
        return new InitialWorldSnapshotPackage
        {
            Epoch = epoch,
            ClientList = ClientPackageBuilder.Snapshot(clients),
            Characters = players.Select(player =>
                {
                    var values = new ValuesDictionary();
                    player.Save(values);
                    return values.ToMessagePack();
                }
            ).ToArray(),
            EntityData = EntitySnapshotSerializer.Serialize(project, entities),
            CharacterEntities = EntitySnapshotSerializer.CaptureCharacterEntities(entities)
        };
    }

    public static SubsystemWeatherPackage Capture(SubsystemWeather weather) => new()
    {
        PrecipitationStartTime = weather.PrecipitationStartTime,
        PrecipitationEndTime = weather.PrecipitationEndTime,
        LightningIntensity = weather.LightningIntensity,
        FogStartTime = weather.FogStartTime,
        FogRampTime = weather.FogRampTime,
        FogEndTime = weather.FogEndTime
    };
}
