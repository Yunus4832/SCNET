namespace Game;

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

    public P2PRoomSettings Apply(P2PRoomSettings world, string worldName) => world with
    {
        Enabled = Enabled ?? world.Enabled,
        ControlUrl = ControlUrl ?? world.ControlUrl,
        ServiceName = ControlUrl is null ? world.ServiceName : string.Empty,
        Name = Name ?? (string.IsNullOrEmpty(world.Name) ? worldName : world.Name),
        Description = Description ?? world.Description,
        IsPublic = IsPublic ?? world.IsPublic
    };
}
