namespace ServerSource.Protocol;

public sealed record ServerSourceValidationIssue(
    ServerSourceValidationCode Code,
    string Path,
    string Message);
