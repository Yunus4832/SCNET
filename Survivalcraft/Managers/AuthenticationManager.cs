using System.Security.Cryptography;

using Game.Authentication;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Game.Managers;

/// <summary>Owns instance authentication files; initialization performs no network requests or login.</summary>
public sealed class AuthenticationManager(
    AuthenticationServiceRegistry services,
    AuthenticationGrantStore grants,
    ServerCredentialStore serverCredentials
)
{
    private static AuthenticationManager? _current;

    public static AuthenticationManager Current => _current ??
                                                   throw new InvalidOperationException(
                                                       "Authentication configuration is not initialized."
                                                   );

    public AuthenticationServiceRegistry Services { get; } = services;

    public AuthenticationGrantStore Grants { get; } = grants;

    public ServerCredentialStore ServerCredentials { get; } = serverCredentials;

    public AuthenticationServiceInstallationStatus GetInstallationStatus(AuthenticationServiceEndpoint endpoint)
    {
        if (Services.Services.Any(service =>
                service.Endpoint.BaseUri == endpoint.BaseUri &&
                service.Endpoint.Identity.Address != endpoint.Identity.Address
            ))
        {
            return AuthenticationServiceInstallationStatus.KeyConflict;
        }

        var installed = Services.Find(endpoint.Identity.Address);
        if (installed is null) { return AuthenticationServiceInstallationStatus.Available; }

        if (installed.Endpoint.BaseUri != endpoint.BaseUri)
        {
            return AuthenticationServiceInstallationStatus.EndpointChange;
        }

        return installed.Enabled
            ? AuthenticationServiceInstallationStatus.Installed
            : AuthenticationServiceInstallationStatus.Disabled;
    }

    /// <summary>Call only after confirming the displayed endpoint and public identity. Never enables an existing service.</summary>
    public void InstallConfirmedService(AuthenticationServiceEndpoint endpoint, string source)
    {
        switch (GetInstallationStatus(endpoint))
        {
            case AuthenticationServiceInstallationStatus.Available:
                Services.AddConfirmed(endpoint, source);
                break;
            case AuthenticationServiceInstallationStatus.EndpointChange:
                Grants.RemoveAuthority(endpoint.Identity.Address);
                Services.UpdateConfirmedEndpoint(endpoint.Identity.Address, endpoint);
                break;
            case AuthenticationServiceInstallationStatus.Installed:
            case AuthenticationServiceInstallationStatus.Disabled:
                break;
            case AuthenticationServiceInstallationStatus.KeyConflict:
                throw new InvalidOperationException(
                    "A different public identity already uses this installed endpoint."
                );
        }
    }

    public void RemoveService(string authority)
    {
        Grants.RemoveAuthority(authority);
        Services.Remove(authority);
    }

    public async Task<bool> LogoutAsync(
        ECDsa userKey,
        InstalledAuthenticationService service,
        CancellationToken cancellationToken,
        Func<AuthenticationServiceEndpoint, AuthenticationHttpClient>? createClient = null
    )
    {
        var subject = AuthenticationPublicKey.From(userKey).Address;
        var grant = Grants.Find(subject, service.Endpoint);
        Grants.Remove(subject, service.Endpoint.Identity.Address);
        if (grant is null) { return true; }

        try
        {
            using var client = createClient?.Invoke(service.Endpoint) ?? new AuthenticationHttpClient(service.Endpoint);
            await client.VerifyDiscoveryAsync(cancellationToken);
            var result = await client.RevokeGrantAsync(userKey, grant.Secret, cancellationToken);
            return result.Error == SCNET.Authentication.Protocol.Contracts.AuthenticationError.None;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception error) when (error is HttpRequestException or AuthenticationProtocolException
                                          or AuthenticationRemoteException)
        {
            return false;
        }
    }

    public static void Initialize()
    {
        _current = null;
        var registry = new AuthenticationServiceRegistry(Storage.GetSystemPath(GamePaths.AuthenticationServicesFile));
        var store = new AuthenticationGrantStore(Storage.GetSystemPath(GamePaths.AuthenticationGrantsFile));
        store.Reconcile(registry.Services);
        _current = new AuthenticationManager(
            registry,
            store,
            new ServerCredentialStore(Storage.GetSystemPath(GamePaths.ServerCredentialsFile))
        );
    }
}
