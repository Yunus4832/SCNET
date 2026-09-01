using Engine.FileStorage;

namespace Engine.Test.FileStorage;

public sealed class FilePickerTest
{
    [Fact]
    public async Task FacadeSupportsSingleMultipleSaveCancellationErrorsAndRepeatedCalls()
    {
        var implementation = new TestFilePicker();
        FilePicker.Register(implementation);

        var single = await FilePicker.PickFilesAsync(new FilePickerRequest([".scpkg"]));
        var multiple = await FilePicker.PickFilesAsync(new FilePickerRequest([".scpkg"], true));
        var target = await FilePicker.PickSaveTargetAsync(new FileSaveRequest("package.scpkg"));
        await using (var output = await target!.OpenWriteAsync(CancellationToken.None))
            await output.WriteAsync("saved"u8.ToArray());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FilePicker.PickFilesAsync(new FilePickerRequest([]), cancelled.Token));
        implementation.ThrowOnNextRequest = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FilePicker.PickSaveTargetAsync(new FileSaveRequest("failed.scpkg")));

        Assert.Single(single);
        Assert.Equal(2, multiple.Count);
        Assert.Equal(5, implementation.RequestCount);
        Assert.True(FilePicker.IsAvailable);
    }

    private sealed class TestFilePicker : IFilePicker
    {
        public int RequestCount { get; private set; }
        public bool ThrowOnNextRequest { get; set; }

        public Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerRequest request,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            cancellationToken.ThrowIfCancellationRequested();
            MaybeThrow();
            var count = request.AllowMultiple ? 2 : 1;
            return Task.FromResult<IReadOnlyList<PickedFile>>(Enumerable.Range(1, count)
                .Select(index => new PickedFile($"package-{index}.scpkg", null,
                    _ => Task.FromResult<Stream>(new MemoryStream([checked((byte)index)])))).ToArray());
        }

        public Task<PickedSaveTarget?> PickSaveTargetAsync(FileSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            cancellationToken.ThrowIfCancellationRequested();
            MaybeThrow();
            return Task.FromResult<PickedSaveTarget?>(new PickedSaveTarget(request.SuggestedFileName,
                _ => Task.FromResult<Stream>(new MemoryStream())));
        }

        private void MaybeThrow()
        {
            if (!ThrowOnNextRequest) return;
            ThrowOnNextRequest = false;
            throw new InvalidOperationException("picker failed");
        }
    }
}
