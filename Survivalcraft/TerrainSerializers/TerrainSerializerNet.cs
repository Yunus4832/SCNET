namespace Game.TerrainSerializers;

public class TerrainSerializerNet : TerrainSerializer24
{
    private const string _path = "config:NetChunks.tmp";

    public TerrainSerializerNet()
    {
        if (Storage.DirectoryExists(_path))
        {
            Delete(_path);
        }
        else
        {
            Storage.CreateDirectory(_path);
        }

        storage = new RegionFileStorage(this);
        storage.Open(_path);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (Storage.DirectoryExists(_path))
        {
            Delete(_path);
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
