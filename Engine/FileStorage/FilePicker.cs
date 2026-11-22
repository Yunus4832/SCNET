namespace Engine.FileStorage;

/// <summary>访问当前平台启动器所注册文件选择器的进程级入口。</summary>
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
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetImplementation().PickFilesAsync(request, cancellationToken);
    }

    public static Task<PickedSaveTarget?> PickSaveTargetAsync(
        FileSaveRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetImplementation().PickSaveTargetAsync(request, cancellationToken);
    }

    private static IFilePicker GetImplementation()
    {
        return _implementation ?? throw new InvalidOperationException("No platform file picker has been registered.");
    }
}
