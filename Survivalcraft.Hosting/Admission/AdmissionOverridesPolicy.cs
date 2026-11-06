using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Game.Admission;

public static class AdmissionOverridesPolicy
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
}
