using ContentServer.Domain.AuthenticationServices;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Repository.EntityFrameworkCore;

namespace ContentServer.Infrastructure;

public sealed class AuthenticationServiceRegistrationRepository(ContentServerDbContext context)
    : RepositoryBase<AuthenticationServiceRegistration, AuthenticationServiceRegistrationId, ContentServerDbContext>(
        context
    )
{
    private readonly ContentServerDbContext _context = context;

    public Task<AuthenticationServiceRegistration?> FindAsync(
        AuthenticationServiceRegistrationId id,
        CancellationToken cancellationToken
    )
    {
        return _context.AuthenticationServices.FirstOrDefaultAsync(
            registration => registration.Id == id,
            cancellationToken
        );
    }

    public Task<bool> HasUrlAsync(
        string url,
        AuthenticationServiceRegistrationId? excludedId,
        CancellationToken cancellationToken
    )
    {
        return _context.AuthenticationServices.AnyAsync(
            registration => registration.Id != excludedId &&
                            registration.ServiceUrl == url,
            cancellationToken
        );
    }
}
