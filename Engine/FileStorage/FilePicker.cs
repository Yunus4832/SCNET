namespace Engine.FileStorage;

/// <summary>
/// Process-wide entry point for the file picker registered by the active platform starter.
/// </summary>
public static class FilePicker
{
    private static IFilePicker? _implementation;

    public static bool IsAvailable => _implementation is not null;

    public static void Register(IFilePicker implementation)
    {
        _implementation = implementation ?? throw new ArgumentNullException(nameof(implementation));
    }

    public static Task<IReadOnlyList<PickedFile>> PickFilesAsync(
        FilePickerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetImplementation().PickFilesAsync(request, cancellationToken);
    }

    public static Task<PickedSaveTarget?> PickSaveTargetAsync(
        FileSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetImplementation().PickSaveTargetAsync(request, cancellationToken);
    }

    private static IFilePicker GetImplementation()
    {
        return _implementation
               ?? throw new InvalidOperationException("No platform file picker has been registered.");
    }
}
