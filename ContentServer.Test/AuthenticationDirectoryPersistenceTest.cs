using ContentServer.Application.Commands;
using ContentServer.Domain.Administration;
using ContentServer.Domain.AuthenticationServices;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using MediatR;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;

namespace ContentServer.Test;

public sealed class AuthenticationDirectoryPersistenceTest : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "scnet-auth-directory-" + Guid.NewGuid().ToString("N")
    );

    [Fact]
    public async Task CommandsPersistPublicIdentitySnapshotsAndRejectOwnershipOrStaleRevisions()
    {
        await using var factory = CreateFactory();
        var (id, publisherId) = await SubmitAsync(factory);
        var administratorId = new AdministratorId(Guid.NewGuid());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var db = scope.ServiceProvider.GetRequiredService<ContentServerDbContext>();
            Assert.Equal(
                AuthenticationServiceMutationResult.NotFound,
                await mediator.Send(
                    new EditAuthenticationServiceCommand(
                        id,
                        new PublisherId(Guid.NewGuid()),
                        1,
                        "Intruder",
                        AuthenticationServiceRegistrationTest.Endpoint().BaseUri,
                        null,
                        DateTimeOffset.UtcNow
                    )
                )
            );
            Assert.Equal(
                AuthenticationServiceMutationResult.Conflict,
                await mediator.Send(
                    new ReviewAuthenticationServiceCommand(id, 0, administratorId, true, null, DateTimeOffset.UtcNow)
                )
            );
            Assert.Equal(
                AuthenticationServiceMutationResult.Completed,
                await mediator.Send(
                    new ReviewAuthenticationServiceCommand(
                        id,
                        1,
                        administratorId,
                        true,
                        "Approved",
                        DateTimeOffset.UtcNow
                    )
                )
            );
            var audit = await db.ReviewRecords.SingleAsync(record => record.TargetType == "AuthenticationService");
            Assert.Equal("Approved", audit.Decision);
            Assert.Equal(administratorId, audit.AdministratorId);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentServerDbContext>();
            var registration = await db.AuthenticationServices.SingleAsync();
            Assert.Equal(publisherId, registration.PublisherId);
            Assert.Equal(AuthenticationServiceRegistrationStatus.Active, registration.Status);
            Assert.Equal(
                new long[] { 1, 2 },
                registration.Audit.OrderBy(entry => entry.Revision).Select(entry => entry.Revision)
            );
            Assert.Equal("Submitted", registration.Audit.Single(entry => entry.Revision == 1).Action);
        }
    }

    [Fact]
    public async Task BothCommandAndDatabaseRejectDuplicateUrls()
    {
        await using var factory = CreateFactory();
        var (_, publisherId) = await SubmitAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentServerDbContext>();
        var original = await db.AuthenticationServices.SingleAsync();
        var endpoint = new Uri(original.ServiceUrl);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(
                new SubmitAuthenticationServiceCommand(publisherId, "Duplicate", endpoint, null, DateTimeOffset.UtcNow)
            )
        );
        db.AuthenticationServices.Add(
            AuthenticationServiceRegistration.Submit(publisherId, "Bypass", endpoint, null, DateTimeOffset.UtcNow)
        );
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.IsType<SqliteException>(error.InnerException);
        Assert.Equal(19, ((SqliteException)error.InnerException!).SqliteErrorCode);
        await using var verify = factory.Services.CreateAsyncScope();
        Assert.Equal(
            1,
            await verify.ServiceProvider.GetRequiredService<ContentServerDbContext>().AuthenticationServices
                .CountAsync()
        );
    }

    [Fact]
    public async Task FailedReviewAuditRollsBackListingAndOwnedSnapshotTogether()
    {
        await using var factory = CreateFactory();
        var (id, _) = await SubmitAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentServerDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER RejectAuthReview BEFORE INSERT ON ReviewRecords WHEN NEW.TargetType = 'AuthenticationService' BEGIN SELECT RAISE(ABORT, 'fixture audit rollback'); END;"
            );
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await Assert.ThrowsAsync<DbUpdateException>(() => mediator.Send(
                    new ReviewAuthenticationServiceCommand(
                        id,
                        1,
                        new AdministratorId(Guid.NewGuid()),
                        true,
                        null,
                        DateTimeOffset.UtcNow
                    )
                )
            );
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentServerDbContext>();
            var registration = await db.AuthenticationServices.SingleAsync();
            Assert.Equal(AuthenticationServiceRegistrationStatus.Pending, registration.Status);
            Assert.Equal(1, registration.Revision);
            Assert.Single(registration.Audit);
            Assert.Empty(await db.ReviewRecords.ToArrayAsync());
        }
    }

    [Fact]
    public async Task ConcurrentEditsDoNotOverwriteReviewedIdentityOrAppendConflictingSnapshots()
    {
        await using var factory = CreateFactory();
        await SubmitAsync(factory);
        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();
        var firstDb = first.ServiceProvider.GetRequiredService<ContentServerDbContext>();
        var secondDb = second.ServiceProvider.GetRequiredService<ContentServerDbContext>();
        var left = await firstDb.AuthenticationServices.SingleAsync();
        var right = await secondDb.AuthenticationServices.SingleAsync();
        var endpoint = AuthenticationServiceRegistrationTest.Endpoint().BaseUri;
        Assert.True(left.Edit("First", endpoint, null, 1, DateTimeOffset.UtcNow));
        Assert.True(
            right.Edit(
                "Second",
                AuthenticationServiceRegistrationTest.Endpoint().BaseUri,
                null,
                1,
                DateTimeOffset.UtcNow
            )
        );
        await firstDb.SaveChangesAsync();
        var conflict = await Assert.ThrowsAnyAsync<DbUpdateException>(() => secondDb.SaveChangesAsync());
        Assert.True(
            conflict is DbUpdateConcurrencyException ||
            conflict.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 }
        );
        await using var verify = factory.Services.CreateAsyncScope();
        var saved = await verify.ServiceProvider.GetRequiredService<ContentServerDbContext>().AuthenticationServices
            .SingleAsync();
        Assert.Equal("First", saved.Name);
        Assert.Equal(endpoint.AbsoluteUri, saved.ServiceUrl);
        Assert.Equal(2, saved.Audit.Count);
    }

    private static async Task<(AuthenticationServiceRegistrationId Id, PublisherId PublisherId)> SubmitAsync(
        WebApplicationFactory<Program> factory
    )
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentServerDbContext>();
        var publisher = Publisher.Apply(
            "Publisher",
            "test@example.test",
            null,
            "test",
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow
        );
        db.Publishers.Add(publisher);
        await db.SaveChangesAsync();
        var id = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
            new SubmitAuthenticationServiceCommand(
                publisher.Id,
                "Authority",
                AuthenticationServiceRegistrationTest.Endpoint().BaseUri,
                null,
                DateTimeOffset.UtcNow
            )
        );
        return (id, publisher.Id);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ContentServer:DatabasePath"] = Path.Combine(_directory, "directory.db"),
                            ["ContentServer:PackageStoragePath"] = Path.Combine(_directory, "packages")
                        }
                    )
                );
            }
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) { Directory.Delete(_directory, true); }
    }
}
