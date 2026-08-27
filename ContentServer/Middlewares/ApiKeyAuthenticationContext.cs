using ContentServer.Domain.Administration;
using ContentServer.Domain.Publishers;

namespace ContentServer.Middlewares;

public sealed class ApiKeyAuthenticationContext
{
    public AdministratorId? AdministratorId { get; private set; }

    public PublisherId? PublisherId { get; private set; }

    public AdministratorId RequireAdministratorId() =>
        AdministratorId
        ?? throw new InvalidOperationException("Administrator authentication middleware was not applied.");

    public PublisherId RequirePublisherId() =>
        PublisherId
        ?? throw new InvalidOperationException("Publisher authentication middleware was not applied.");

    internal void SetAdministrator(AdministratorId administratorId)
    {
        AdministratorId = administratorId;
        PublisherId = null;
    }

    internal void SetPublisher(PublisherId publisherId)
    {
        PublisherId = publisherId;
        AdministratorId = null;
    }
}
