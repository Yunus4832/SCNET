using ContentServer.Domain.Administration;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;
using ContentServer.Utils;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Middlewares;

public sealed class ApiKeyAuthenticationMiddleware(
    ContentServerDbContext db,
    ApiKeyAuthenticationContext authenticationContext
) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments("/api/v1/administrator"))
        {
            var administrator = await AuthenticateAdministratorAsync(context.Request, context.RequestAborted);
            if (administrator is null)
            {
                throw new KnownException("unauthorized", StatusCodes.Status401Unauthorized);
            }

            authenticationContext.SetAdministrator(administrator.Value.Id);
        }
        else if (context.Request.Path.StartsWithSegments("/api/v1/admin"))
        {
            var administrator = await AuthenticateAdministratorAsync(context.Request, context.RequestAborted);
            if (administrator is null)
            {
                throw new KnownException("unauthorized", StatusCodes.Status401Unauthorized);
            }

            if (administrator.Value.Status != AdministratorStatus.Active)
            {
                throw new KnownException("administrator_not_active", StatusCodes.Status403Forbidden);
            }

            authenticationContext.SetAdministrator(administrator.Value.Id);
        }
        else if (context.Request.Path.StartsWithSegments("/api/v1/publisher"))
        {
            var publisherId = await AuthenticatePublisherAsync(context.Request, context.RequestAborted);
            if (publisherId is null)
            {
                throw new KnownException("unauthorized", StatusCodes.Status401Unauthorized);
            }

            authenticationContext.SetPublisher(publisherId);
        }

        await next(context);
    }


    private async Task<PublisherId?> AuthenticatePublisherAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var hash = GetKeyHash(request);
        if (hash is null)
        {
            return null;
        }

        var key = await db.PublisherKeys.FirstOrDefaultAsync(
            item => item.KeyHash == hash && item.RevokedAt == null, cancellationToken);
        if (key is null)
        {
            return null;
        }

        var publisher = await db.Publishers.FindAsync([key.PublisherId], cancellationToken);
        if (publisher is null)
        {
            return null;
        }

        key.Touch(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return publisher.Id;
    }

    private async Task<AuthenticatedAdministrator?> AuthenticateAdministratorAsync(
        HttpRequest request,
        CancellationToken cancellationToken
    )
    {
        var hash = GetKeyHash(request);
        if (hash is null)
        {
            return null;
        }

        var key = await db.AdministratorKeys.FirstOrDefaultAsync(
            item => item.KeyHash == hash && item.RevokedAt == null, cancellationToken);
        if (key is null)
        {
            return null;
        }

        var administrator = await db.Administrators.FindAsync([key.AdministratorId], cancellationToken);
        if (administrator is null)
        {
            return null;
        }

        key.Touch(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return new AuthenticatedAdministrator(administrator.Id, administrator.Status);
    }

    private static string? GetKeyHash(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var key = authorization[7..].Trim();
        return key.Length == 0 ? null : ApiKeyUtility.Hash(key);
    }

    private readonly record struct AuthenticatedAdministrator(
        AdministratorId Id,
        AdministratorStatus Status);
}
