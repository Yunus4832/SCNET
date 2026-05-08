namespace Game.TerrainSerializers.NetWork;

public class TerrainSerializerNet : TerrainSerializer24
{
    private const string _path = "config:NetChunks.tmp";

    public TerrainSerializerNet()
    {
        if (Engine.FileStorage.Storage.DirectoryExists(_path))
        {
            Delete(_path);
        }
        else
        {
            Engine.FileStorage.Storage.CreateDirectory(_path);
        }

        storage = new RegionFileStorage(this);
        storage.Open(_path);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (Engine.FileStorage.Storage.DirectoryExists(_path))
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
        foreach (var d in Engine.FileStorage.Storage.ListDirectoryNames(path))
        {
            var dd = Engine.FileStorage.Storage.CombinePaths(path, d);
            Delete(dd);
        }

        foreach (var f in Engine.FileStorage.Storage.ListFileNames(path))
        {
            var ff = Engine.FileStorage.Storage.CombinePaths(path, f);
            Engine.FileStorage.Storage.DeleteFile(ff);
        }
    }
}
