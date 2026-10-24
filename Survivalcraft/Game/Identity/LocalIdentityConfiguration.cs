using System.Security.Cryptography;
using System.Xml.Linq;

using Engine.Network.Security;

namespace Game.Identity;

/// <summary>Instance-owned user and server identities, stored as unencrypted PKCS#8 keys.</summary>
public sealed class LocalIdentityConfiguration
{
    private readonly Lock _gate = new();

    private readonly Action<XElement> _save;

    private XElement _configuration;

    public LocalIdentityConfiguration(XElement? configuration, Action<XElement> save)
    {
        _save = save ?? throw new ArgumentNullException(nameof(save));
        _configuration = configuration is null ? new XElement("LocalIdentities") : new XElement(configuration);
        if (_configuration.Name != "LocalIdentities" ||
            _configuration.HasAttributes ||
            _configuration.Elements().Count() > 2)
        {
            throw new InvalidDataException("Invalid local identity configuration.");
        }

        foreach (var entry in _configuration.Elements())
        {
            if (entry.Name != "Player" && entry.Name != "Server" ||
                entry.HasElements ||
                _configuration.Elements(entry.Name).Count() != 1)
            {
                throw new InvalidDataException("Invalid or duplicate identity purpose.");
            }

            using var identity = ReadIdentity(entry);
        }
    }

    /// <summary>Prepare missing identities during instance initialization, before any game session starts.</summary>
    public void Initialize()
    {
        lock (_gate)
        {
            var replacement = new XElement(_configuration);
            foreach (var purpose in new[] { IdentityPurpose.Player, IdentityPurpose.Server })
            {
                var name = PurposeName(purpose);
                if (replacement.Element(name) is not null) { continue; }

                using var identity = NetworkIdentity.Create();
                replacement.Add(WriteIdentity(name, identity));
            }

            if (XNode.DeepEquals(replacement, _configuration)) { return; }

            _save(new XElement(replacement));
            _configuration = replacement;
        }
    }

    public NetworkIdentity OpenPlayerIdentity() => OpenIdentity(IdentityPurpose.Player);

    public NetworkIdentity OpenServerIdentity() => OpenIdentity(IdentityPurpose.Server);

    private NetworkIdentity OpenIdentity(IdentityPurpose purpose)
    {
        lock (_gate)
        {
            var entry = _configuration.Element(PurposeName(purpose)) ??
                        throw new InvalidOperationException("This identity has not been initialized.");
            return ReadIdentity(entry);
        }
    }

    public string? GetAddress(IdentityPurpose purpose)
    {
        lock (_gate) { return _configuration.Element(PurposeName(purpose))?.Attribute("Address")?.Value; }
    }

    public byte[] ExportBackup(IdentityPurpose purpose, ReadOnlySpan<char> password)
    {
        using var identity = OpenIdentity(purpose);
        return IdentityBackup.Export(identity, password);
    }

    public string InspectBackup(IdentityPurpose purpose, ReadOnlySpan<byte> backup, ReadOnlySpan<char> password)
    {
        _ = PurposeName(purpose);
        using var identity = IdentityBackup.Import(backup, password);
        return identity.PublicIdentity.Address;
    }

    public string ImportBackup(
        IdentityPurpose purpose,
        ReadOnlySpan<byte> backup,
        ReadOnlySpan<char> password,
        string? expectedAddress
    )
    {
        lock (_gate)
        {
            var name = PurposeName(purpose);
            if (!StringComparer.Ordinal.Equals(GetAddress(purpose), expectedAddress))
            {
                throw new InvalidOperationException("The configured identity changed during import.");
            }

            using var identity = IdentityBackup.Import(backup, password);
            var replacement = new XElement(_configuration);
            replacement.Element(name)?.Remove();
            replacement.Add(WriteIdentity(name, identity));
            _save(new XElement(replacement));
            _configuration = replacement;
            return identity.PublicIdentity.Address;
        }
    }

    private static XElement WriteIdentity(string name, NetworkIdentity identity)
    {
        var bytes = identity.ExportPrivateKey();
        try
        {
            return new XElement(
                name,
                new XAttribute("Address", identity.PublicIdentity.Address),
                new XAttribute("PrivateKey", Convert.ToBase64String(bytes))
            );
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static NetworkIdentity ReadIdentity(XElement entry)
    {
        if (entry.Attributes().Count() != 2 ||
            entry.Attribute("Address") is not { } address ||
            entry.Attribute("PrivateKey") is not { } privateKey)
        {
            throw new InvalidDataException("An identity requires its address and PKCS#8 private key.");
        }

        var bytes = Convert.FromBase64String(privateKey.Value);
        try
        {
            var identity = NetworkIdentity.Import(bytes);
            if (!StringComparer.Ordinal.Equals(identity.PublicIdentity.Address, address.Value))
            {
                identity.Dispose();
                throw new CryptographicException("The private key does not match the configured identity address.");
            }

            return identity;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static string PurposeName(IdentityPurpose purpose) => purpose switch
    {
        IdentityPurpose.Player => "Player",
        IdentityPurpose.Server => "Server",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose))
    };
}
