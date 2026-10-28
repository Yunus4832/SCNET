using System.Xml.Linq;

using Game.Admission;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Game.Managers;

/// <summary>Validates admission settings and resolves effective policies at server startup.</summary>
public static class ServerAdmissionManager
{
    public static ServerAdmissionOverrides Merge(ServerAdmissionOverrides saved, ServerAdmissionOverrides request)
    {
        // An explicit mode starts a new policy intent; do not carry saved credentials from another mode.
        var previous = request.Mode is null ? saved : new ServerAdmissionOverrides();
        return new ServerAdmissionOverrides
        {
            Mode = request.Mode ?? previous.Mode,
            PasswordFile = request.PasswordFile ?? previous.PasswordFile,
            AuthenticationUrl = request.AuthenticationUrl ?? previous.AuthenticationUrl,
            AuthenticationKey = request.AuthenticationKey ?? previous.AuthenticationKey,
            AuthenticationName = request.AuthenticationName ?? previous.AuthenticationName
        };
    }

    public static void Validate(ServerAdmissionOverrides overrides)
    {
        try
        {
            if (overrides.Mode is { } mode && !Enum.IsDefined(mode)) { throw new FormatException(); }

            if (overrides.Mode is { } explicitMode &&
                (explicitMode != ServerAdmissionMode.Password && overrides.PasswordFile is not null ||
                 explicitMode != ServerAdmissionMode.External &&
                 (overrides.AuthenticationUrl is not null ||
                  overrides.AuthenticationKey is not null ||
                  overrides.AuthenticationName is not null)))
            {
                throw new FormatException();
            }

            if (overrides.PasswordFile is { } file && (string.IsNullOrWhiteSpace(file) || file.Length > 4096))
            {
                throw new FormatException();
            }

            if (overrides.AuthenticationUrl is { } url) { AuthenticationServiceEndpoint.ParseBaseUri(url); }

            if (overrides.AuthenticationKey is { } key) { AuthenticationPublicKey.Parse(key); }

            if (overrides.AuthenticationName is { } name &&
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

    public static ServerAdmissionPolicy Resolve(ServerAdmissionOverrides overrides, ServerAdmissionPolicy saved)
    {
        Validate(overrides);
        if (overrides.IsEmpty) { return saved; }

        var mode = overrides.Mode ?? saved.Mode;
        if (mode != ServerAdmissionMode.Password && overrides.PasswordFile is not null ||
            mode != ServerAdmissionMode.External &&
            (overrides.AuthenticationUrl is not null ||
             overrides.AuthenticationKey is not null ||
             overrides.AuthenticationName is not null))
        {
            throw new AdmissionConfigurationException("Admission credentials do not match the selected mode.");
        }

        if (mode == ServerAdmissionMode.Open) { return ServerAdmissionPolicy.Open; }

        if (mode == ServerAdmissionMode.Password)
        {
            if (overrides.PasswordFile is null)
            {
                return saved.Mode == mode
                    ? saved
                    : throw new AdmissionConfigurationException("Password mode requires --server-password-file.");
            }

            var path = Path.IsPathRooted(overrides.PasswordFile) ? overrides.PasswordFile :
                overrides.PasswordFile.Contains(':') ? Storage.GetSystemPath(overrides.PasswordFile) :
                Path.GetFullPath(overrides.PasswordFile);
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

            return ServerAdmissionPolicy.WithPassword(value);
        }

        var url = overrides.AuthenticationUrl ?? saved.Authority?.BaseUri.AbsoluteUri;
        var keyValue = overrides.AuthenticationKey ?? saved.Authority?.Description.PublicKey;
        if (url is null || keyValue is null)
        {
            throw new AdmissionConfigurationException(
                "External admission requires an authentication URL and public key."
            );
        }

        return ServerAdmissionPolicy.WithAuthority(
            new AuthenticationServiceEndpoint(
                new AuthenticationServiceDescription(
                    AuthenticationProtocol.Version,
                    overrides.AuthenticationName ?? saved.Authority?.Description.DisplayName ?? new Uri(url).Host,
                    url,
                    keyValue
                )
            )
        );
    }

    public static XElement SerializeOverrides(ServerAdmissionOverrides overrides)
    {
        Validate(overrides);
        var node = new XElement("AdmissionOverrides");
        if (overrides.Mode is { } mode) { node.Add(new XAttribute("Mode", mode)); }

        foreach (var (name, value) in new[]
                 {
                     ("PasswordFile", overrides.PasswordFile), ("AuthenticationUrl", overrides.AuthenticationUrl),
                     ("AuthenticationKey", overrides.AuthenticationKey),
                     ("AuthenticationName", overrides.AuthenticationName)
                 })
        {
            if (value is not null) { node.Add(new XAttribute(name, value)); }
        }

        return node;
    }

    public static ServerAdmissionOverrides DeserializeOverrides(XElement? node)
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
        Validate(result);
        return result;
    }
}
