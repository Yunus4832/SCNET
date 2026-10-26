using System.Security.Cryptography;

using ContentServer.Domain.Administration;
using ContentServer.Domain.AuthenticationServices;
using ContentServer.Domain.Publishers;

using NetCorePal.Extensions.Domain;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace ContentServer.Test;

public sealed class AuthenticationServiceRegistrationTest
{
    [Fact]
    public void EditsHidePublicListingAndStaleReviewCannotApproveUnseenDescription()
    {
        var now = DateTimeOffset.UtcNow;
        var endpoint = Endpoint().BaseUri;
        var administrator = new AdministratorId(Guid.NewGuid());
        var registration = AuthenticationServiceRegistration.Submit(
            new PublisherId(Guid.NewGuid()),
            "Authority",
            endpoint,
            null,
            now
        );
        Assert.Equal(1, registration.Revision);
        Assert.True(registration.Review(true, administrator, "Reviewed", 1, now));
        Assert.Equal(AuthenticationServiceRegistrationStatus.Active, registration.Status);
        Assert.True(registration.Edit("Updated", Endpoint().BaseUri, "Changed", 2, now));
        Assert.Equal(AuthenticationServiceRegistrationStatus.Pending, registration.Status);
        Assert.Null(registration.ReviewedAt);
        Assert.Null(registration.ReviewMessage);
        Assert.False(registration.Review(true, administrator, null, 2, now));
        Assert.False(registration.Disable(administrator, null, 3, now));
        Assert.Equal(3, registration.Revision);
        Assert.True(registration.Review(false, administrator, "Rejected", 3, now));
        Assert.False(registration.Review(true, administrator, null, 4, now));
        Assert.Equal(endpoint.AbsoluteUri, registration.Audit.First().ServiceUrl);
        Assert.Equal("Authority", registration.Audit.First().Name);
        Assert.Equal("Updated", registration.Audit.Last().Name);
        Assert.Equal(administrator, registration.Audit.Last().AdministratorId);
        Assert.Equal("Rejected", registration.Audit.Last().Message);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, registration.Audit.Select(entry => entry.Revision));
    }

    [Fact]
    public void WithdrawalDisableAndDeleteHaveDistinctAuditedLifecycles()
    {
        var now = DateTimeOffset.UtcNow;
        var endpoint = Endpoint().BaseUri;
        var administrator = new AdministratorId(Guid.NewGuid());
        var registration = AuthenticationServiceRegistration.Submit(
            new PublisherId(Guid.NewGuid()),
            "Authority",
            endpoint,
            null,
            now
        );
        Assert.True(registration.Withdraw(1, now));
        Assert.False(registration.Withdraw(2, now));
        Assert.True(registration.Edit("Authority", endpoint, null, 2, now));
        Assert.True(registration.Review(true, administrator, null, 3, now));
        Assert.True(registration.Disable(administrator, "Unavailable", 4, now));
        Assert.False(registration.Disable(administrator, null, 5, now));
        Assert.True(registration.Delete(administrator, 5, now));
        Assert.Equal(new Deleted(true), registration.Deleted);
        Assert.False(registration.Edit("Resurrect", endpoint, null, 6, now));
        Assert.False(registration.Review(true, administrator, null, 6, now));
        Assert.False(registration.Withdraw(6, now));
        Assert.False(registration.Delete(administrator, 6, now));
        Assert.Equal(
            new[] { "Submitted", "Withdrawn", "Edited", "Approved", "Disabled", "Deleted" },
            registration.Audit.Select(entry => entry.Action)
        );
    }

    [Fact]
    public void InvalidEditDoesNotPartiallyChangeTheListingOrAdvanceItsRevision()
    {
        var now = DateTimeOffset.UtcNow;
        var endpoint = Endpoint().BaseUri;
        var registration = AuthenticationServiceRegistration.Submit(
            new PublisherId(Guid.NewGuid()),
            "Original",
            endpoint,
            null,
            now
        );
        Assert.Throws<ArgumentException>(() => registration.Edit("Invalid\nName", Endpoint().BaseUri, null, 1, now));
        Assert.Equal("Original", registration.Name);
        Assert.Equal(endpoint.AbsoluteUri, registration.ServiceUrl);
        Assert.Equal(1, registration.Revision);
        Assert.Single(registration.Audit);
    }

    internal static AuthenticationServiceEndpoint Endpoint(string url = "https://authority.example/")
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new AuthenticationServiceEndpoint(
            new AuthenticationServiceDescription(
                AuthenticationProtocol.Version,
                "Authority",
                url,
                AuthenticationPublicKey.From(key).EncodedKey
            )
        );
    }
}
