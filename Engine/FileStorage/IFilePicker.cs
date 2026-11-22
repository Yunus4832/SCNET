namespace Engine.FileStorage;

/// <summary>提供平台文件选择器接口，由平台启动器注册实现。</summary>
public interface IFilePicker
{
    Task<IReadOnlyList<PickedFile>> PickFilesAsync(
        FilePickerRequest request,
        CancellationToken cancellationToken = default
    );

    Task<PickedSaveTarget?> PickSaveTargetAsync(
        FileSaveRequest request,
        CancellationToken cancellationToken = default
    );
}
