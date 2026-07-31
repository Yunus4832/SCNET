using Engine.FileStorage;

namespace Engine.Test.FileStorage;

public class StorageTest : IDisposable
{
    private readonly string _directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public StorageTest()
    {
        Storage.RegisterRoot("test", new FileSystemStorageRoot(_directoryPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RegisteredRootSupportsVirtualFileOperations()
    {
        Storage.WriteAllText("test:folder/file.txt", "hello");

        Assert.True(Storage.FileExists("test:folder/file.txt"));
        Assert.Equal("hello", Storage.ReadAllText("test:folder/file.txt"));
        Assert.Equal(["file.txt"], Storage.ListFileNames("test:folder"));
        Assert.Equal(Path.Combine(_directoryPath, "folder", "file.txt"),
            Storage.GetSystemPath("test:folder/file.txt"));
    }

    [Fact]
    public void FileSystemRootRejectsEscapingPath()
    {
        Assert.Throws<InvalidOperationException>(() => Storage.GetSystemPath("test:../outside.txt"));
    }

    [Fact]
    public void UnregisteredRootIsReportedClearly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Storage.FileExists("missing:file.txt"));

        Assert.Contains("missing:", exception.Message);
    }
}
