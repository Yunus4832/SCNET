namespace Game.Servers;

public sealed record StoredServerEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public int Order { get; init; }

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
