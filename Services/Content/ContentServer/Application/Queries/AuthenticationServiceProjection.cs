using ContentServer.Domain.AuthenticationServices;
using ContentServer.Domain.Publishers;

namespace ContentServer.Application.Queries;

internal static class AuthenticationServiceProjection
{
    internal static IQueryable<AuthenticationServiceDto> Project(
        IQueryable<AuthenticationServiceRegistration> registrations,
        IQueryable<Publisher> publishers
    )
    {
        return registrations.Join(
            publishers,
            registration => registration.PublisherId,
            publisher => publisher.Id,
            (registration, publisher) => new AuthenticationServiceDto(
                registration.Id,
                registration.PublisherId,
                publisher.DisplayName,
                registration.Name,
                registration.ServiceUrl,
                registration.Description,
                registration.Status,
                registration.Revision,
                registration.ReviewMessage,
                registration.CreatedAt,
                registration.UpdatedAt,
                registration.ReviewedAt
            )
        );
    }
}
