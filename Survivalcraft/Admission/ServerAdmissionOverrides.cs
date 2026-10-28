namespace Game.Admission;

/// <summary>Recoverable startup overrides contain public provider data and a password FILE reference only.</summary>
public sealed record ServerAdmissionOverrides
{
    public ServerAdmissionMode? Mode { get; init; }

    public string? PasswordFile { get; init; }

    public string? AuthenticationUrl { get; init; }

    public string? AuthenticationKey { get; init; }

    public string? AuthenticationName { get; init; }

    public bool IsEmpty => Mode is null &&
                           PasswordFile is null &&
                           AuthenticationUrl is null &&
                           AuthenticationKey is null &&
                           AuthenticationName is null;
}
