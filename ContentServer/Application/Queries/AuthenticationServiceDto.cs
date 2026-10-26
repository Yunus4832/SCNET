using ContentServer.Domain.AuthenticationServices;
using ContentServer.Domain.Publishers;

namespace ContentServer.Application.Queries;

public sealed record AuthenticationServiceDto(
    AuthenticationServiceRegistrationId Id,
    PublisherId PublisherId,
    string PublisherName,
    string Name,
    string ServiceUrl,
    string? Description,
    AuthenticationServiceRegistrationStatus Status,
    long Revision,
    string? ReviewMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt
);
