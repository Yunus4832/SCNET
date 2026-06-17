namespace Game.TerrainSerializers;

public class TerrainSerializerNet : TerrainSerializer24
{
    private static string Path => GamePaths.NetChunksTempFile;

    public TerrainSerializerNet()
    {
        if (Storage.DirectoryExists(Path))
        {
            Delete(Path);
        }
        else
        {
            Storage.CreateDirectory(Path);
        }

        storage = new RegionFileStorage(this);
        storage.Open(Path);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (Storage.DirectoryExists(Path))
        {
            Delete(Path);
        }
    }

    /// <summary>
    /// 轮询删除文件夹下的文件
    /// </summary>
    /// <param name="path"></param>
    public void Delete(string path)
    {
        foreach (var d in Storage.ListDirectoryNames(path))
        {
            var dd = Storage.CombinePaths(path, d);
            Delete(dd);
        }

        foreach (var f in Storage.ListFileNames(path))
        {
            var ff = Storage.CombinePaths(path, f);
            Storage.DeleteFile(ff);
        }
    }
}
