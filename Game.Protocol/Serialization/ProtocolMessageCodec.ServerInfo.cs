using Game.Protocol.Connection;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static ServerInfoPackage ReadServerInfo(ProtocolReader reader)
    {
        if (reader.ReadBoolean()) { return new ServerInfoPackage(true); }
        var message = new ServerInfoPackage
        {
            ProtocolVersion = reader.ReadInt32(),
            ClientCount = reader.ReadUInt16(),
            MaxPlayerCount = reader.ReadUInt16(),
            GameMode = reader.ReadByte(),
            TimeOfDay = reader.ReadSingle(),
            TemporaryRepositories = ReadTemporaryRepositories(reader),
            RequiredModProfile = ReadProfile(reader),
            Season = reader.ReadByte(),
            TimeOfSeason = reader.ReadSingle()
        };
        if (!float.IsFinite(message.TimeOfDay) || !float.IsFinite(message.TimeOfSeason))
        { throw new InvalidDataException("Invalid server state."); }
        return message;
    }

    private static void WriteServerInfo(ServerInfoPackage message, ProtocolWriter writer)
    {
        writer.Write(message.RequestInfo);
        if (message.RequestInfo) { return; }
        writer.Write(message.ProtocolVersion);
        writer.Write(message.ClientCount);
        writer.Write(message.MaxPlayerCount);
        writer.Write(message.GameMode);
        writer.Write(message.TimeOfDay);
        WriteTemporaryRepositories(writer, message.TemporaryRepositories);
        WriteProfile(writer, message.RequiredModProfile);
        writer.Write(message.Season);
        writer.Write(message.TimeOfSeason);
    }

    private static RequiredModProfile? ReadProfile(ProtocolReader reader)
    {
        if (!reader.ReadBoolean())
        {
            return null;
        }

        var id = reader.ReadString();
        var packages = new List<RequiredModPackage>();
        var count = reader.ReadUInt16();
        if (count > ProtocolLimits.MaximumRequiredMods)
        {
            throw new InvalidDataException("Too many required mods were declared.");
        }

        for (var i = 0; i < count; i++)
        {
            packages.Add(new RequiredModPackage(reader.ReadString(), reader.ReadString(), reader.ReadString()));
        }

        var profile = new RequiredModProfile(id, packages.ToArray());
        try
        {
            ValidateProfile(profile);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The server declared an invalid required mod profile.", exception);
        }

        return profile;
    }

    private static void WriteProfile(ProtocolWriter writer, RequiredModProfile? profile)
    {
        writer.Write(profile != null);
        if (profile == null)
        {
            return;
        }

        ValidateProfile(profile);
        writer.Write(profile.Id);
        writer.Write((ushort)profile.Packages.Count);
        foreach (var package in profile.Packages)
        {
            writer.Write(package.ModId);
            writer.Write(package.Version);
            writer.Write(package.PackageHash);
        }
    }

    private static IReadOnlyList<ContentRepositoryDescriptor> ReadTemporaryRepositories(ProtocolReader reader)
    {
        var count = reader.ReadUInt16();
        if (count > ProtocolLimits.MaximumTemporaryRepositories)
        {
            throw new InvalidDataException("Too many temporary content repositories were declared.");
        }

        var repositories = new List<ContentRepositoryDescriptor>(count);
        for (var index = 0; index < count; index++)
        {
            var idText = reader.ReadString();
            var baseUrl = reader.ReadString();
            var priority = reader.ReadInt32();
            if (!Guid.TryParseExact(idText, "D", out var id) || baseUrl.Length > ProtocolLimits.MaximumRepositoryUrlLength)
            {
                throw new InvalidDataException("A temporary content repository descriptor is invalid.");
            }

            repositories.Add(new ContentRepositoryDescriptor(id, baseUrl, priority));
        }

        return ValidateRepositories(repositories);
    }

    private static void WriteTemporaryRepositories(ProtocolWriter writer,
        IEnumerable<ContentRepositoryDescriptor> repositories)
    {
        var normalized = ValidateRepositories(repositories);
        writer.Write((ushort)normalized.Count);
        foreach (var repository in normalized)
        {
            writer.Write(repository.Id.ToString("D"));
            writer.Write(repository.BaseUrl);
            writer.Write(repository.Priority);
        }
    }

    private static void ValidateProfile(RequiredModProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id) || profile.Id.Length > ProtocolLimits.MaximumModProfileIdLength || profile.Packages.Count > ProtocolLimits.MaximumRequiredMods)
        { throw new ArgumentException("Invalid required mod profile."); }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var package in profile.Packages)
        {
            if (string.IsNullOrWhiteSpace(package.ModId) || package.ModId.Length > ProtocolLimits.MaximumModIdLength || !ids.Add(package.ModId) ||
                string.IsNullOrWhiteSpace(package.Version) || package.Version.Length > ProtocolLimits.MaximumModVersionLength ||
                package.PackageHash.Length != 64 || package.PackageHash.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            { throw new ArgumentException("Invalid required mod package."); }
        }
    }

    private static IReadOnlyList<ContentRepositoryDescriptor> ValidateRepositories(IEnumerable<ContentRepositoryDescriptor> repositories)
    {
        var values = repositories.ToArray();
        if (values.Length > ProtocolLimits.MaximumTemporaryRepositories) { throw new InvalidDataException("Too many temporary repositories."); }
        var ids = new HashSet<Guid>();
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (value.Id == Guid.Empty || !ids.Add(value.Id) || value.BaseUrl.Length > ProtocolLimits.MaximumRepositoryUrlLength || value.Priority is < ProtocolLimits.MinimumRepositoryPriority or > ProtocolLimits.MaximumRepositoryPriority ||
                !Uri.TryCreate(value.BaseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
                !addresses.Add(uri.AbsoluteUri.TrimEnd('/')))
            { throw new InvalidDataException("Invalid temporary repository."); }
        }
        return values;
    }
}
