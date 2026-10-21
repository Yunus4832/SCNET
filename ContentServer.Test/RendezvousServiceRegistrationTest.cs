using ContentServer.Domain.Administration;
using ContentServer.Domain.Publishers;
using ContentServer.Domain.RendezvousServices;

namespace ContentServer.Test;

public sealed class RendezvousServiceRegistrationTest
{
    [Fact]
    public void EditingPublishedServiceRequiresReviewAndDeletionIsExplicit()
    {
        var now = DateTimeOffset.UtcNow;
        var registration = RendezvousServiceRegistration.Submit(
            new PublisherId(Guid.NewGuid()),
            "Signal",
            new Uri("https://signal.example/"),
            null,
            now
        );
        Assert.Equal(RendezvousServiceRegistrationStatus.Pending, registration.Status);
        Assert.True(registration.Review(true, new AdministratorId(Guid.NewGuid()), null, now));
        Assert.False(registration.Review(true, new AdministratorId(Guid.NewGuid()), null, now));
        registration.Edit("New signal", new Uri("https://other.example/"), "New", now);
        Assert.Equal(RendezvousServiceRegistrationStatus.Pending, registration.Status);
        Assert.Null(registration.ReviewedAt);
        Assert.False(registration.Disable(now));
        Assert.True(registration.Review(true, new AdministratorId(Guid.NewGuid()), null, now));
        Assert.True(registration.Disable(now));
        Assert.Equal(RendezvousServiceRegistrationStatus.Disabled, registration.Status);
        registration.Delete(now);
        Assert.Equal(new NetCorePal.Extensions.Domain.Deleted(true), registration.Deleted);
    }
}
