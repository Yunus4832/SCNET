using Game.Admission;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;

namespace Game.Managers;

/// <summary>Validates admission settings and resolves effective policies at server startup.</summary>
public static class ServerAdmissionManager
{
    public static ServerAdmissionPolicy Resolve(ServerAdmissionOverrides overrides, ServerAdmissionPolicy saved)
    {
        AdmissionOverridesPolicy.Validate(overrides);
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
}
