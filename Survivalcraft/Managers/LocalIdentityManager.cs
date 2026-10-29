using System.Security.Cryptography;
using System.Xml.Linq;

using Engine.Core.Security;
using Engine.Network.Security;

using Game.Identity;

namespace Game.Managers;

/// <summary>Owns instance identities, default selection, backup operations and transactional persistence.</summary>
public sealed class LocalIdentityManager
{
    public const int MaximumIdentityCount = 128;

    public const int MaximumNameLength = 80;

    private static LocalIdentityManager? _current;

    private readonly Lock _gate = new();

    private readonly Action<LocalIdentitySettings> _save;

    private LocalIdentitySettings _settings;

    public static LocalIdentityManager Current => _current ??
                                                  throw new InvalidOperationException(
                                                      "Local identities have not been initialized."
                                                  );

    public LocalIdentityManager(LocalIdentitySettings settings, Action<LocalIdentitySettings> save)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(save);
        _settings = Snapshot(settings);
        _save = save;
        foreach (var purpose in Enum.GetValues<IdentityPurpose>())
        {
            var entries = _settings.Identities.Where(entry => entry.Purpose == purpose).ToArray();
            if (entries.Length > MaximumIdentityCount ||
                entries.Select(entry => entry.Address).Distinct(StringComparer.Ordinal).Count() != entries.Length ||
                (entries.Length == 0
                    ? GetSelectedAddress(purpose) is not null
                    : !entries.Any(entry => entry.Address == GetSelectedAddress(purpose))))
            {
                throw new InvalidDataException("Invalid identity collection or default selection.");
            }
        }

        foreach (var entry in _settings.Identities)
        {
            if (!Enum.IsDefined(entry.Purpose) ||
                entry.Name.Length > MaximumNameLength ||
                entry.Name.Any(char.IsControl))
            {
                throw new InvalidDataException("Invalid identity metadata.");
            }

            using var identity = ReadIdentity(entry);
        }
    }

    public static void Initialize()
    {
        _current = null;
        var path = Storage.GetSystemPath(GamePaths.LocalIdentitiesFile);
        XElement? configuration = null;
        if (File.Exists(path))
        {
            if (new FileInfo(path).Length > 262144)
            {
                throw new InvalidDataException("Identity configuration is too large.");
            }

            configuration = XElement.Load(path);
        }

        var manager = new LocalIdentityManager(ReadSettings(configuration), Save);
        manager.EnsureDefaults();
        _current = manager;
    }

    public static bool CanManage => GameManager.Project is null &&
                                    !Network.CommonLib.Net.IsTransportRunning &&
                                    Network.CommonLib.Net.CurrentStage == Network.NetNode.Stage.NotConnected;

    public void EnsureDefaults()
    {
        lock (_gate)
        {
            var replacement = _settings;
            foreach (var purpose in Enum.GetValues<IdentityPurpose>())
            {
                if (replacement.Identities.Any(entry => entry.Purpose == purpose)) { continue; }

                using var identity = NetworkIdentity.Create();
                replacement = replacement with
                {
                    Identities =
                    [
                        .. replacement.Identities,
                        WriteIdentity(purpose, purpose == IdentityPurpose.Player ? "UserId" : "ServerId", identity)
                    ]
                };
                replacement = SelectDefault(replacement, purpose, identity.PublicIdentity.Address);
            }

            if (!ReferenceEquals(replacement, _settings)) { Commit(replacement); }
        }
    }

    public IReadOnlyList<LocalIdentityEntry> GetIdentities(IdentityPurpose purpose)
    {
        _ = PurposeName(purpose);
        lock (_gate)
        {
            return _settings.Identities.Where(entry => entry.Purpose == purpose)
                .Select(entry => new LocalIdentityEntry(purpose, entry.Name, entry.Address)).ToArray();
        }
    }

    public string? GetSelectedAddress(IdentityPurpose purpose)
    {
        lock (_gate)
        {
            return purpose switch
            {
                IdentityPurpose.Player => _settings.SelectedPlayerAddress,
                IdentityPurpose.Server => _settings.SelectedServerAddress,
                _ => throw new ArgumentOutOfRangeException(nameof(purpose))
            };
        }
    }

    public NetworkIdentity OpenPlayerIdentity() => OpenIdentity(IdentityPurpose.Player);

    public NetworkIdentity OpenServerIdentity(string? address = null) => OpenIdentity(IdentityPurpose.Server, address);

    private NetworkIdentity OpenIdentity(IdentityPurpose purpose, string? address = null)
    {
        lock (_gate)
        {
            address = address is null ? GetSelectedAddress(purpose) : IdentityAddress.Normalize(address);
            return ReadIdentity(FindEntry(purpose, address));
        }
    }

    public void Select(IdentityPurpose purpose, string address)
    {
        lock (_gate)
        {
            _ = FindEntry(purpose, address);
            Commit(SelectDefault(_settings, purpose, address));
        }
    }

    public string Create(IdentityPurpose purpose, string name)
    {
        using var identity = NetworkIdentity.Create();
        return Add(purpose, name, identity);
    }

    public void Rename(IdentityPurpose purpose, string address, string name)
    {
        name = ValidateName(name);
        lock (_gate)
        {
            var selected = FindEntry(purpose, address);
            Commit(
                _settings with
                {
                    Identities = _settings.Identities.Select(entry =>
                        ReferenceEquals(entry, selected) ? entry with { Name = name } : entry
                    ).ToArray()
                }
            );
        }
    }

    public void Delete(IdentityPurpose purpose, string address)
    {
        lock (_gate)
        {
            if (address == GetSelectedAddress(purpose))
            {
                throw new InvalidOperationException("Select another default identity before deleting this one.");
            }

            var selected = FindEntry(purpose, address);
            Commit(
                _settings with
                {
                    Identities = _settings.Identities.Where(entry => !ReferenceEquals(entry, selected)).ToArray()
                }
            );
        }
    }

    public byte[] ExportBackup(IdentityPurpose purpose, string address, ReadOnlySpan<char> password)
    {
        using var identity = OpenIdentity(purpose, address);
        return IdentityBackup.Export(identity, password);
    }

    public static string InspectBackup(ReadOnlySpan<byte> backup, ReadOnlySpan<char> password)
    {
        using var identity = IdentityBackup.Import(backup, password);
        return identity.PublicIdentity.Address;
    }

    public string ImportBackup(
        IdentityPurpose purpose,
        ReadOnlySpan<byte> backup,
        ReadOnlySpan<char> password,
        string name
    )
    {
        using var identity = IdentityBackup.Import(backup, password);
        return Add(purpose, name, identity);
    }

    private string Add(IdentityPurpose purpose, string name, NetworkIdentity identity)
    {
        name = ValidateName(name);
        lock (_gate)
        {
            if (GetSelectedAddress(purpose) is null)
            {
                throw new InvalidOperationException("Identities have not been initialized.");
            }

            var entries = _settings.Identities.Where(entry => entry.Purpose == purpose).ToArray();
            if (entries.Length >= MaximumIdentityCount ||
                entries.Any(entry => entry.Address == identity.PublicIdentity.Address))
            {
                throw new InvalidOperationException("Identity already exists or the identity limit has been reached.");
            }

            Commit(_settings with { Identities = [.. _settings.Identities, WriteIdentity(purpose, name, identity)] });
            return identity.PublicIdentity.Address;
        }
    }

    private void Commit(LocalIdentitySettings replacement)
    {
        var snapshot = Snapshot(replacement);
        _save(Snapshot(snapshot));
        _settings = snapshot;
    }

    private StoredLocalIdentity FindEntry(IdentityPurpose purpose, string? address)
    {
        return _settings.Identities.SingleOrDefault(entry => entry.Purpose == purpose && entry.Address == address) ??
               throw new InvalidOperationException("The requested identity is not available in this instance.");
    }

    private static LocalIdentitySettings Snapshot(LocalIdentitySettings settings)
    {
        return settings with { Identities = settings.Identities.ToArray() };
    }

    private static LocalIdentitySettings SelectDefault(
        LocalIdentitySettings settings,
        IdentityPurpose purpose,
        string address
    )
    {
        return purpose switch
        {
            IdentityPurpose.Player => settings with { SelectedPlayerAddress = address },
            IdentityPurpose.Server => settings with { SelectedServerAddress = address },
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };
    }

    private static string ValidateName(string name)
    {
        name = name.Trim();
        if (name.Length is 0 or > MaximumNameLength || name.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Identity name must be 1–80 characters without control characters.",
                nameof(name)
            );
        }

        return name;
    }

    private static StoredLocalIdentity WriteIdentity(IdentityPurpose purpose, string name, NetworkIdentity identity)
    {
        var bytes = identity.ExportPrivateKey();
        try
        {
            return new StoredLocalIdentity(
                purpose,
                name,
                identity.PublicIdentity.Address,
                Convert.ToBase64String(bytes)
            );
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static NetworkIdentity ReadIdentity(StoredLocalIdentity entry)
    {
        var bytes = Convert.FromBase64String(entry.PrivateKey);
        try
        {
            var identity = NetworkIdentity.Import(bytes);
            if (identity.PublicIdentity.Address != entry.Address)
            {
                identity.Dispose();
                throw new CryptographicException("The private key does not match the configured identity address.");
            }

            return identity;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static LocalIdentitySettings ReadSettings(XElement? configuration)
    {
        if (configuration is null) { return new LocalIdentitySettings(); }

        if (configuration.Name != "LocalIdentities" ||
            configuration.HasAttributes ||
            configuration.Elements().Count() > 2)
        {
            throw new InvalidDataException("Invalid local identity configuration.");
        }

        var settings = new LocalIdentitySettings();
        var entries = new List<StoredLocalIdentity>();
        foreach (var group in configuration.Elements())
        {
            if ((group.Name != "Player" && group.Name != "Server") ||
                configuration.Elements(group.Name).Count() != 1 ||
                group.Attributes().Count() != 1 ||
                group.Attribute("Selected") is not { } selected ||
                !group.HasElements)
            {
                throw new InvalidDataException("Invalid identity collection.");
            }

            var purpose = group.Name == "Player" ? IdentityPurpose.Player : IdentityPurpose.Server;
            settings = SelectDefault(settings, purpose, selected.Value);
            foreach (var entry in group.Elements())
            {
                if (entry.Name != "Identity" ||
                    entry.HasElements ||
                    entry.Attributes().Count() != 3 ||
                    entry.Attribute("Name") is not { } name ||
                    entry.Attribute("Address") is not { } address ||
                    entry.Attribute("PrivateKey") is not { } key)
                {
                    throw new InvalidDataException("An identity requires a name, address and PKCS#8 private key.");
                }

                entries.Add(new StoredLocalIdentity(purpose, name.Value, address.Value, key.Value));
            }
        }

        return settings with { Identities = entries.ToArray() };
    }

    internal static XElement WriteSettings(LocalIdentitySettings settings)
    {
        var root = new XElement("LocalIdentities");
        foreach (var purpose in Enum.GetValues<IdentityPurpose>())
        {
            var entries = settings.Identities.Where(entry => entry.Purpose == purpose).ToArray();
            if (entries.Length == 0) { continue; }

            var address = purpose == IdentityPurpose.Player
                ? settings.SelectedPlayerAddress
                : settings.SelectedServerAddress;
            root.Add(
                new XElement(
                    PurposeName(purpose),
                    new XAttribute("Selected", address!),
                    entries.Select(entry =>
                        new XElement(
                            "Identity",
                            new XAttribute("Name", entry.Name),
                            new XAttribute("Address", entry.Address),
                            new XAttribute("PrivateKey", entry.PrivateKey)
                        )
                    )
                )
            );
        }

        return root;
    }

    private static string PurposeName(IdentityPurpose purpose)
    {
        return purpose switch
        {
            IdentityPurpose.Player => "Player",
            IdentityPurpose.Server => "Server",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };
    }

    private static void Save(LocalIdentitySettings settings)
    {
        var path = Storage.GetSystemPath(GamePaths.LocalIdentitiesFile);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions
                { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(temporary, options))
            {
                WriteSettings(settings).Save(stream);
                stream.Flush(true);
            }

            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) { File.Delete(temporary); }
        }
    }
}
