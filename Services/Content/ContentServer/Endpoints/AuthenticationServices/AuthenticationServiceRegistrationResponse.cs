using ContentServer.Application.Queries;

namespace ContentServer.Endpoints.AuthenticationServices;

public sealed record AuthenticationServiceRegistrationResponse(
    string Id,
    string PublisherId,
    string PublisherName,
    string Name,
    string ServiceUrl,
    string? Description,
    string Status,
    long Revision,
    string? ReviewMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt
)
{
    internal static AuthenticationServiceRegistrationResponse From(AuthenticationServiceDto item)
    {
        return new AuthenticationServiceRegistrationResponse(
            item.Id.ToString(),
            item.PublisherId.ToString(),
            item.PublisherName,
            item.Name,
            item.ServiceUrl,
            item.Description,
            item.Status.ToString().ToLowerInvariant(),
            item.Revision,
            item.ReviewMessage,
            item.CreatedAt,
            item.UpdatedAt,
            item.ReviewedAt
        );
    }
}
