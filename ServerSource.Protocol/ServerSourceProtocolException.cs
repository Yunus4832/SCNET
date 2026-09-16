namespace ServerSource.Protocol;

public sealed class ServerSourceProtocolException : Exception
{
    public ServerSourceProtocolException(string message)
        : base(message)
    {
    }

    public ServerSourceProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public IReadOnlyList<ServerSourceValidationIssue> ValidationIssues { get; init; } = [];
}
