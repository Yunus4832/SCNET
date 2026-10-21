namespace ServerSource.Protocol;

public sealed class ServerSourceValidationResult(IReadOnlyList<ServerSourceValidationIssue> issues)
{
    public bool IsValid => Issues.Count == 0;

    public IReadOnlyList<ServerSourceValidationIssue> Issues { get; } = issues;
}
