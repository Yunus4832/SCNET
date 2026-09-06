using Game.Content;
using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

/// <summary>
///     基础包模板复制
/// </summary>
public class ServerInfoPackage : IPackage
{
    public ushort ClientCount;

    public GameMode GameMode;

    public ushort MaxPlayerCount;

    public IReadOnlyList<ContentRepository> TemporaryRepositories = [];

    public ModProfile? RequiredModProfile;

    public bool RequestInfo;

    /// <summary>
    ///     季节
    /// </summary>
    public Season Season;

    public float TimeOfDay;

    /// <summary>
    ///     季节进度
    /// </summary>
    public float TimeOfSeason;

    public string Version = string.Empty;

    public int Ping;

    public byte ID => (byte)PackageType.ServerInfo;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ServerInfoPackage()
    {
    }


    public ServerInfoPackage(bool requestInfo)
    {
        RequestInfo = requestInfo;
        if (RequestInfo)
        {
            return;
        }


        if (GameManager.Project is null)
        {
            throw new InvalidOperationException("GameManager is not ready");
        }

        var project = GameManager.Project;
        var subsystemGameInfo = project.FindSubsystem<SubsystemGameInfo>(true)!;
        var subsystemTimeOfDay = project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        var subsystemSeasons = project.FindSubsystem<SubsystemSeasons>(true)!;

        Version = VersionsManager.ProtocolVersion;
        ClientCount = (ushort)CommonLib.Net.ClientCount;
        MaxPlayerCount = subsystemGameInfo.WorldSettings.MaxOnlinePlayerCount;
        GameMode = subsystemGameInfo.WorldSettings.GameMode;
        TimeOfDay = subsystemTimeOfDay.CalculateTimeOfDay();
        RequiredModProfile = CurrentModRuntime.Value?.CreateServerRequiredProfile();
        TemporaryRepositories = SettingsManager.Current.ContentRepositories
            .Where(repository => repository.IsEnabled).ToArray();
        Season = subsystemSeasons.Season;
        TimeOfSeason = subsystemSeasons.TimeOfSeason;
    }


    public void ReadData(PackageStreamReader reader)
    {
        RequestInfo = reader.ReadBoolean();
        if (RequestInfo)
        {
            return;
        }

        Version = reader.ReadString();
        ClientCount = reader.ReadUInt16();
        MaxPlayerCount = reader.ReadUInt16();
        GameMode = reader.ReadEnum<GameMode>();
        TimeOfDay = reader.ReadSingle();
        TemporaryRepositories = ReadTemporaryRepositories(reader);
        RequiredModProfile = ReadProfile(reader);

        Season = (Season)reader.ReadInt32();
        TimeOfSeason = reader.ReadSingle();
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(RequestInfo);
        if (RequestInfo)
        {
            return;
        }

        writer.Write(Version);
        writer.Write(ClientCount);
        writer.Write(MaxPlayerCount);
        writer.WriteEnum(GameMode);
        writer.Write(TimeOfDay);
        WriteTemporaryRepositories(writer, TemporaryRepositories);
        WriteProfile(writer, RequiredModProfile);
        writer.Write((int)Season);
        writer.Write(TimeOfSeason);
    }

    private static ModProfile? ReadProfile(PackageStreamReader reader)
    {
        if (reader.BaseStream.Position >= reader.BaseStream.Length || !reader.ReadBoolean())
        {
            return null;
        }

        var profile = new ModProfile
        {
            Id = reader.ReadString(),
            Packages = []
        };
        var count = reader.ReadUInt16();
        if (count > ModProfileValidation.MaximumRequirements)
        {
            throw new InvalidDataException("Too many required mods were declared.");
        }

        for (var i = 0; i < count; i++)
        {
            profile.Packages.Add(new ModPackageRequirement
            {
                ModId = reader.ReadString(),
                Version = reader.ReadString(),
                PackageHash = reader.ReadString()
            });
        }

        try
        {
            ModProfileValidation.Validate(profile);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The server declared an invalid required mod profile.", exception);
        }

        return profile;
    }

    private static void WriteProfile(PackageStreamWriter writer, ModProfile? profile)
    {
        writer.Write(profile != null);
        if (profile == null)
        {
            return;
        }

        ModProfileValidation.Validate(profile);
        writer.Write(profile.Id);
        writer.Write((ushort)profile.Packages.Count);
        foreach (var package in profile.Packages)
        {
            writer.Write(package.ModId);
            writer.Write(package.Version);
            writer.Write(package.PackageHash);
        }
    }

    private static IReadOnlyList<ContentRepository> ReadTemporaryRepositories(PackageStreamReader reader)
    {
        var count = reader.ReadUInt16();
        if (count > TemporaryContentRepositories.MaximumCount)
        {
            throw new InvalidDataException("Too many temporary content repositories were declared.");
        }

        var repositories = new List<ContentRepository>(count);
        for (var index = 0; index < count; index++)
        {
            var idText = reader.ReadString();
            var baseUrl = reader.ReadString();
            var priority = reader.ReadInt32();
            if (!Guid.TryParseExact(idText, "D", out var id) || baseUrl.Length > TemporaryContentRepositories.MaximumUrlLength)
            {
                throw new InvalidDataException("A temporary content repository descriptor is invalid.");
            }

            repositories.Add(new ContentRepository
            {
                Id = id,
                Name = $"Server Repository {index + 1}",
                BaseUrl = baseUrl,
                Priority = priority
            });
        }

        return TemporaryContentRepositories.Validate(repositories);
    }

    private static void WriteTemporaryRepositories(PackageStreamWriter writer,
        IEnumerable<ContentRepository> repositories)
    {
        var normalized = TemporaryContentRepositories.Validate(repositories);
        writer.Write((ushort)normalized.Count);
        foreach (var repository in normalized)
        {
            writer.Write(repository.Id.ToString("D"));
            writer.Write(repository.BaseUrl);
            writer.Write(repository.Priority);
        }
    }
}
