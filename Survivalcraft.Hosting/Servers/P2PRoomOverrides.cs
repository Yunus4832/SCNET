namespace Game.Servers;

public sealed record P2PRoomOverrides
{
    public bool? Enabled { get; init; }

    public string? ControlUrl { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public bool? IsPublic { get; init; }

    public P2PRoomOverrides Merge(P2PRoomOverrides request) => new()
    {
        Enabled = request.Enabled ?? Enabled,
        ControlUrl = request.ControlUrl ?? ControlUrl,
        Name = request.Name ?? Name,
        Description = request.Description ?? Description,
        IsPublic = request.IsPublic ?? IsPublic
    };
}
