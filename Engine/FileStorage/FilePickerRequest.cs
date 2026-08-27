namespace Engine.FileStorage;

public sealed record FilePickerRequest(
    IReadOnlyList<string> Extensions,
    bool AllowMultiple = false,
    string? Title = null);
