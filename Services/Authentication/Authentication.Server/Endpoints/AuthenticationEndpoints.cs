using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;
using SCNET.Authentication.Server.Infrastructure;

namespace SCNET.Authentication.Server.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationProtocol(this WebApplication app)
    {
        app.MapGet(
            "/" + AuthenticationProtocol.DiscoveryPath,
            (ProviderRuntime runtime) => Results.Json(runtime.Service.Description)
        );
        app.MapPost(
            "/" + AuthenticationProtocol.TransactionsPath,
            async (SignedAuthenticationRequest request, ProviderRuntime runtime) =>
            {
                var now = DateTimeOffset.UtcNow;
                var verified = await Verify<AuthenticationTransactionRequest>(
                    request,
                    AuthenticationProtocol.TransactionsPath,
                    runtime,
                    now
                );
                return runtime.Execute(authority => authority.Begin(verified.Subject, verified.Data, now));
            }
        );
        app.MapPost(
            "/" + AuthenticationProtocol.PollPath,
            async (SignedAuthenticationRequest request, ProviderRuntime runtime) =>
            {
                var now = DateTimeOffset.UtcNow;
                var verified = await Verify<AuthenticationTransactionCommand>(
                    request,
                    AuthenticationProtocol.PollPath,
                    runtime,
                    now
                );
                return runtime.Execute(authority => authority.Poll(verified.Subject, verified.Data.TransactionId, now));
            }
        );
        app.MapPost(
            "/" + AuthenticationProtocol.CancelPath,
            async (SignedAuthenticationRequest request, ProviderRuntime runtime) =>
            {
                var now = DateTimeOffset.UtcNow;
                var verified = await Verify<AuthenticationTransactionCommand>(
                    request,
                    AuthenticationProtocol.CancelPath,
                    runtime,
                    now
                );
                return runtime.Execute(
                    authority => authority.Cancel(verified.Subject, verified.Data.TransactionId, now),
                    true
                );
            }
        );
        app.MapPost(
            "/" + AuthenticationProtocol.RevokePath,
            async (SignedAuthenticationRequest request, ProviderRuntime runtime) =>
            {
                var now = DateTimeOffset.UtcNow;
                var verified = await Verify<AuthenticationGrantRevocation>(
                    request,
                    AuthenticationProtocol.RevokePath,
                    runtime,
                    now
                );
                runtime.Execute(
                    authority =>
                    {
                        authority.Revoke(verified.Subject, verified.Data.Grant);
                        return true;
                    },
                    true
                );
                return new AuthenticationErrorResponse(AuthenticationError.None);
            }
        );
    }

    private static async Task<VerifiedAuthenticationRequest<T>> Verify<T>(
        SignedAuthenticationRequest request,
        string path,
        ProviderRuntime runtime,
        DateTimeOffset now
    )
    {
        var proof = await AuthenticationRequestProofs.ValidateAsync<T>(
            request,
            runtime.PublicKey.Address,
            runtime.Service.GetUri(path),
            now
        );
        if (!runtime.ReplayCache.TryAccept(proof.Subject, proof.RequestId, proof.ExpiresAt, now))
        {
            throw new AuthenticationRemoteException(AuthenticationError.InvalidProof);
        }

        return proof;
    }
}
