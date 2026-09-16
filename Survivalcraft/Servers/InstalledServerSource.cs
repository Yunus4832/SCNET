namespace Game.Servers;

public sealed record InstalledServerSource
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string? RegistrationId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ApiUrl { get; init; } = string.Empty;

    public bool IsEnabled { get; init; } = true;

    public int Order { get; init; }
}
