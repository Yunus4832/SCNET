namespace Engine.FileStorage;

/// <summary>
///     Provides access to the platform file picker. Implementations are registered by the platform starter.
/// </summary>
public interface IFilePicker
{
    Task<IReadOnlyList<PickedFile>> PickFilesAsync(
        FilePickerRequest request,
        CancellationToken cancellationToken = default);

    Task<PickedSaveTarget?> PickSaveTargetAsync(
        FileSaveRequest request,
        CancellationToken cancellationToken = default);
}
