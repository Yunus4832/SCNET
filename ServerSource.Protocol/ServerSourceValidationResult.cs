namespace ServerSource.Protocol;

public sealed class ServerSourceValidationResult
{
    public ServerSourceValidationResult(IReadOnlyList<ServerSourceValidationIssue> issues)
    {
        Issues = issues;
    }

    public bool IsValid => Issues.Count == 0;

    public IReadOnlyList<ServerSourceValidationIssue> Issues { get; }
}
