namespace Engine.FileStorage;

public sealed record FileSaveRequest(
    string SuggestedFileName,
    string? ContentType = null,
    string? Title = null);
