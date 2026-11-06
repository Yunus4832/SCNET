namespace Game.Identity;

/// <summary>Private instance data; use public identity summaries for UI and diagnostics.</summary>
public sealed record LocalIdentitySettings
{
    public IReadOnlyList<StoredLocalIdentity> Identities { get; init; } = [];

    public string? SelectedPlayerAddress { get; init; }

    public string? SelectedServerAddress { get; init; }
}
