namespace Engine.FileStorage;

public interface IStorageRoot
{
    long FreeSpace { get; }

    bool FileExists(string path);

    bool DirectoryExists(string path);

    long GetFileSize(string path);

    DateTime GetFileLastWriteTime(string path);

    Stream OpenFile(string path, OpenFileMode openFileMode);

    void DeleteFile(string path);

    void MoveFile(string sourcePath, string destinationPath);

    void CreateDirectory(string path);

    void DeleteDirectory(string path, bool recursive);

    void MoveDirectory(string sourcePath, string destinationPath);

    IEnumerable<string> ListFileNames(string path);

    IEnumerable<string> ListDirectoryNames(string path);

    string GetSystemPath(string path);
}
