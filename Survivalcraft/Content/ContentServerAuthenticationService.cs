using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Http;

namespace Game.Content;

public sealed record ContentServerAuthenticationService(
    string Id,
    string Name,
    string ServiceUrl,
    string? Description,
    DateTimeOffset? ReviewedAt
)
{
    public Uri Validate()
    {
        try
        {
            if (!Guid.TryParseExact(Id, "D", out _) ||
                string.IsNullOrWhiteSpace(Name) ||
                Name.Length > 100 ||
                Name.Any(char.IsControl) ||
                Description?.Length > 1000 ||
                ReviewedAt is null)
            {
                throw new InvalidDataException("Invalid authentication directory entry.");
            }

            return AuthenticationServiceEndpoint.ParseBaseUri(ServiceUrl);
        }
        catch (Exception error) when (error is ArgumentException or FormatException
                                          or AuthenticationProtocolException)
        {
            throw new InvalidDataException("Invalid authentication directory source endpoint.", error);
        }
    }
}
