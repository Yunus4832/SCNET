using System.Xml.Linq;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Game.Admission;

/// <summary>Recoverable startup overrides contain public provider data and a password FILE reference only.</summary>
public sealed record ServerAdmissionOverrides
{
    public ServerAdmissionMode? Mode { get; init; }

    public string? PasswordFile { get; init; }

    public string? AuthenticationUrl { get; init; }

    public string? AuthenticationKey { get; init; }

    public string? AuthenticationName { get; init; }

    public bool IsEmpty => Mode is null &&
                           PasswordFile is null &&
                           AuthenticationUrl is null &&
                           AuthenticationKey is null &&
                           AuthenticationName is null;

    public static ServerAdmissionOverrides FromEnvironment()
    {
        static string? Read(string name)
        {
            return Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
        }

        if (Read("SCNET_SERVER_PASSWORD") is not null)
        {
            throw new AdmissionConfigurationException(
                "Use SCNET_SERVER_PASSWORD_FILE, not a plaintext environment password."
            );
        }

        var mode = Read("SCNET_ADMISSION")?.ToLowerInvariant();
        var file = Read("SCNET_SERVER_PASSWORD_FILE");
        var result = new ServerAdmissionOverrides
        {
            Mode = mode switch
            {
                null => null, "open" => ServerAdmissionMode.Open, "password" => ServerAdmissionMode.Password,
                "external" => ServerAdmissionMode.External,
                _ => throw new AdmissionConfigurationException("SCNET_ADMISSION requires open, password or external.")
            },
            PasswordFile = file is null ? null :
                Path.IsPathRooted(file) || !file.Contains(':') ? Path.GetFullPath(file) : file,
            AuthenticationUrl = Read("SCNET_AUTHENTICATION_URL"), AuthenticationKey = Read("SCNET_AUTHENTICATION_KEY"),
            AuthenticationName = Read("SCNET_AUTHENTICATION_NAME")
        };
        result.Validate();
        return result;
    }

    public ServerAdmissionOverrides Merge(ServerAdmissionOverrides request)
    {
        // An explicit mode starts a new policy intent; do not carry saved credentials from another mode.
        var previous = request.Mode is null ? this : new ServerAdmissionOverrides();
        return new ServerAdmissionOverrides
        {
            Mode = request.Mode ?? previous.Mode,
            PasswordFile = request.PasswordFile ?? previous.PasswordFile,
            AuthenticationUrl = request.AuthenticationUrl ?? previous.AuthenticationUrl,
            AuthenticationKey = request.AuthenticationKey ?? previous.AuthenticationKey,
            AuthenticationName = request.AuthenticationName ?? previous.AuthenticationName
        };
    }

    public void Validate()
    {
        try
        {
            if (Mode is { } mode && !Enum.IsDefined(mode)) { throw new FormatException(); }

            if (Mode is { } explicitMode &&
                (explicitMode != ServerAdmissionMode.Password && PasswordFile is not null ||
                 explicitMode != ServerAdmissionMode.External &&
                 (AuthenticationUrl is not null || AuthenticationKey is not null || AuthenticationName is not null)))
            {
                throw new FormatException();
            }

            if (PasswordFile is { } file && (string.IsNullOrWhiteSpace(file) || file.Length > 4096))
            {
                throw new FormatException();
            }

            if (AuthenticationUrl is { } url) { AuthenticationServiceEndpoint.ParseBaseUri(url); }

            if (AuthenticationKey is { } key) { AuthenticationPublicKey.Parse(key); }

            if (AuthenticationName is { } name &&
                (string.IsNullOrWhiteSpace(name) || name.Length > 128 || name.Any(char.IsControl)))
            {
                throw new FormatException();
            }
        }
        catch (Exception error) when (error is ArgumentException or FormatException
                                          or System.Security.Cryptography.CryptographicException
                                          or AuthenticationProtocolException)
        {
            throw new AdmissionConfigurationException("Invalid admission override configuration.", error);
        }
    }

    public ServerAdmissionConfiguration Apply(ServerAdmissionConfiguration saved)
    {
        Validate();
        if (IsEmpty) { return saved; }

        var mode = Mode ?? saved.Mode;
        if (mode != ServerAdmissionMode.Password && PasswordFile is not null ||
            mode != ServerAdmissionMode.External &&
            (AuthenticationUrl is not null || AuthenticationKey is not null || AuthenticationName is not null))
        {
            throw new AdmissionConfigurationException("Admission credentials do not match the selected mode.");
        }

        if (mode == ServerAdmissionMode.Open) { return ServerAdmissionConfiguration.Open; }

        if (mode == ServerAdmissionMode.Password)
        {
            if (PasswordFile is null)
            {
                return saved.Mode == mode
                    ? saved
                    : throw new AdmissionConfigurationException("Password mode requires --server-password-file.");
            }

            var path = Path.IsPathRooted(PasswordFile) ? PasswordFile :
                PasswordFile.Contains(':') ? Storage.GetSystemPath(PasswordFile) : Path.GetFullPath(PasswordFile);
            if (!File.Exists(path) || new FileInfo(path).Length > 8192)
            {
                throw new AdmissionConfigurationException("Server password file is missing or too large.");
            }

            var value = File.ReadAllText(path);
            if (value.EndsWith("\r\n", StringComparison.Ordinal)) { value = value[..^2]; }
            else if (value.EndsWith('\n')) { value = value[..^1]; }

            if (value.Length is < 1 or > 1024 || value.Any(char.IsControl))
            {
                throw new AdmissionConfigurationException(
                    "Server password file must contain one nonempty line of at most 1024 characters."
                );
            }

            return ServerAdmissionConfiguration.WithPassword(value);
        }

        var url = AuthenticationUrl ?? saved.Authority?.BaseUri.AbsoluteUri;
        var keyValue = AuthenticationKey ?? saved.Authority?.Description.PublicKey;
        if (url is null || keyValue is null)
        {
            throw new AdmissionConfigurationException(
                "External admission requires an authentication URL and public key."
            );
        }

        return ServerAdmissionConfiguration.WithAuthority(
            new AuthenticationServiceEndpoint(
                new AuthenticationServiceDescription(
                    AuthenticationProtocol.Version,
                    AuthenticationName ?? saved.Authority?.Description.DisplayName ?? new Uri(url).Host,
                    url,
                    keyValue
                )
            )
        );
    }

    public XElement ToXml()
    {
        Validate();
        var node = new XElement("AdmissionOverrides");
        if (Mode is { } mode) { node.Add(new XAttribute("Mode", mode)); }

        foreach (var (name, value) in new[]
                 {
                     ("PasswordFile", PasswordFile), ("AuthenticationUrl", AuthenticationUrl),
                     ("AuthenticationKey", AuthenticationKey), ("AuthenticationName", AuthenticationName)
                 })
        {
            if (value is not null) { node.Add(new XAttribute(name, value)); }
        }

        return node;
    }

    public static ServerAdmissionOverrides FromXml(XElement? node)
    {
        if (node is null) { return new ServerAdmissionOverrides(); }

        if (node.Name != "AdmissionOverrides" ||
            node.HasElements ||
            node.Attributes().Any(attribute =>
                attribute.Name.LocalName is not "Mode" and not "PasswordFile" and not "AuthenticationUrl"
                    and not "AuthenticationKey" and not "AuthenticationName"
            ))
        {
            throw new AdmissionConfigurationException("Invalid stored admission overrides.");
        }

        ServerAdmissionMode? mode = null;
        if (node.Attribute("Mode") is { } value)
        {
            if (!Enum.TryParse<ServerAdmissionMode>(value.Value, true, out var parsed) || !Enum.IsDefined(parsed))
            {
                throw new AdmissionConfigurationException("Invalid stored admission mode.");
            }

            mode = parsed;
        }

        var result = new ServerAdmissionOverrides
        {
            Mode = mode, PasswordFile = (string?)node.Attribute("PasswordFile"),
            AuthenticationUrl = (string?)node.Attribute("AuthenticationUrl"),
            AuthenticationKey = (string?)node.Attribute("AuthenticationKey"),
            AuthenticationName = (string?)node.Attribute("AuthenticationName")
        };
        result.Validate();
        return result;
    }
}
