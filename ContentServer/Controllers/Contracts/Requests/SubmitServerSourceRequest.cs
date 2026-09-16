namespace ContentServer.Controllers.Contracts.Requests;

public sealed record SubmitServerSourceRequest(string Name, string ApiUrl, string? Description);
