using System.Security.Cryptography;

using ContentServer.Application.Commands;
using ContentServer.Domain.AuthenticationServices;

using NetCorePal.Extensions.Primitives;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;

namespace ContentServer.Endpoints.AuthenticationServices;

internal static class AuthenticationDirectoryInput
{
    internal static Uri Parse(string name, string serviceUrl, string? description)
    {
        try
        {
            var invalidDescription = description is not null &&
                                     description.Any(character =>
                                         char.IsControl(character) && character is not '\n' and not '\r' and not '\t'
                                     );
            if (string.IsNullOrWhiteSpace(name) ||
                name.Length > 100 ||
                name.Any(char.IsControl) ||
                description?.Length > 1000 ||
                invalidDescription)
            {
                throw new ArgumentException();
            }

            return AuthenticationServiceEndpoint.ParseBaseUri(serviceUrl);
        }
        catch (Exception error) when (error is ArgumentException or FormatException or AuthenticationProtocolException)
        {
            throw new KnownException("invalid_authentication_service_submission", StatusCodes.Status400BadRequest);
        }
    }

    internal static AuthenticationServiceRegistrationStatus? ParseStatus(string? value)
    {
        if (value is null) { return null; }

        if (Enum.TryParse<AuthenticationServiceRegistrationStatus>(value, true, out var status) &&
            Enum.IsDefined(status)) { return status; }

        throw new KnownException("invalid_status", StatusCodes.Status400BadRequest);
    }

    internal static void RequireRevision(long revision)
    {
        if (revision < 1) { throw new KnownException("invalid_revision", StatusCodes.Status400BadRequest); }
    }

    internal static void RequireMessage(string? message)
    {
        if (message?.Length > 1000)
        {
            throw new KnownException("invalid_review_message", StatusCodes.Status400BadRequest);
        }
    }

    internal static void RequireSuccess(AuthenticationServiceMutationResult result)
    {
        if (result == AuthenticationServiceMutationResult.NotFound)
        {
            throw new KnownException("authentication_service_not_found", StatusCodes.Status404NotFound);
        }

        if (result != AuthenticationServiceMutationResult.Completed)
        {
            throw new KnownException("authentication_service_conflict", StatusCodes.Status409Conflict);
        }
    }
}
