namespace ContentServer.Controllers.Contracts.Responses;

public sealed record AdministratorResponse(string AdministratorId, string Name, string Contact, string? Description, string Status, bool IsSuperAdministrator, bool HasActiveKey,
    string? ReviewMessage, DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt);
public sealed record AdministratorApplicationResponse(string AdministratorId, string Status, string ApiKey);
