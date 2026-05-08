using System.Xml.Linq;
using EntitySystem.XmlUtilities;
using Game.TerrainSerializers;

namespace Game.VersionConverts;

public class VersionConverter22To23 : VersionConverter
{
    public override string SourceVersion => "2.2";

    public override string TargetVersion => "2.3";

    public override void ConvertProjectXml(XElement projectNode)
    {
        XmlUtils.SetAttributeValue(projectNode, "Version", TargetVersion);
    }

    public override void ConvertWorld(string directoryName)
    {
        try
        {
            ConvertChunks(directoryName);
            ConvertProject(directoryName);
            foreach (var item in from f in Storage.ListFileNames(directoryName)
                     where Storage.GetExtension(f) == ".new"
                     select f)
            {
                var sourcePath = Storage.CombinePaths(directoryName, item);
                var destinationPath = Storage.CombinePaths(directoryName, Storage.GetFileNameWithoutExtension(item));
                Storage.MoveFile(sourcePath, destinationPath);
            }

            foreach (var item2 in from f in Storage.ListDirectoryNames(directoryName)
                     where Storage.GetExtension(f) == ".new"
                     select f)
            {
                var sourcePath2 = Storage.CombinePaths(directoryName, item2);
                var destinationPath2 = Storage.CombinePaths(directoryName, Storage.GetFileNameWithoutExtension(item2));
                Storage.MoveDirectory(sourcePath2, destinationPath2);
            }

            foreach (var item3 in from f in Storage.ListFileNames(directoryName)
                     where Storage.GetExtension(f) == ".old"
                     select f)
            {
                Storage.DeleteFile(Storage.CombinePaths(directoryName, item3));
            }

            foreach (var item4 in from f in Storage.ListDirectoryNames(directoryName)
                     where Storage.GetExtension(f) == ".old"
                     select f)
            {
                Storage.DeleteDirectoryRecursive(Storage.CombinePaths(directoryName, item4));
            }
        }
        catch (Exception)
        {
            foreach (var item5 in from f in Storage.ListFileNames(directoryName)
                     where Storage.GetExtension(f) == ".old"
                     select f)
            {
                var sourcePath3 = Storage.CombinePaths(directoryName, item5);
                var destinationPath3 = Storage.CombinePaths(directoryName, Storage.GetFileNameWithoutExtension(item5));
                Storage.MoveFile(sourcePath3, destinationPath3);
            }

            foreach (var item6 in from f in Storage.ListDirectoryNames(directoryName)
                     where Storage.GetExtension(f) == ".old"
                     select f)
            {
                var sourcePath4 = Storage.CombinePaths(directoryName, item6);
                var destinationPath4 = Storage.CombinePaths(directoryName, Storage.GetFileNameWithoutExtension(item6));
                Storage.MoveDirectory(sourcePath4, destinationPath4);
            }

            foreach (var item7 in from f in Storage.ListFileNames(directoryName)
                     where Storage.GetExtension(f) == ".new"
                     select f)
            {
                Storage.DeleteFile(Storage.CombinePaths(directoryName, item7));
            }

            foreach (var item8 in from f in Storage.ListDirectoryNames(directoryName)
                     where Storage.GetExtension(f) == ".new"
                     select f)
            {
                Storage.DeleteDirectoryRecursive(Storage.CombinePaths(directoryName, item8));
            }

            throw;
        }
    }

    private void ConvertProject(string directoryName)
    {
        var path = Storage.CombinePaths(directoryName, "Project.xml");
        var path2 = Storage.CombinePaths(directoryName, "Project.xml.new");
        XElement xElement;
        using (var stream = Storage.OpenFile(path, OpenFileMode.Read))
        {
            xElement = XmlUtils.LoadXmlFromStream(stream, null, true);
        }

        ConvertProjectXml(xElement);
        using (var stream2 = Storage.OpenFile(path2, OpenFileMode.Create))
        {
            XmlUtils.SaveXmlToStream(xElement, stream2, null, true);
        }
    }

    private void ConvertChunks(string directoryName)
    {
        var num = Storage.GetFileSize(Storage.CombinePaths(directoryName, "Chunks32h.dat")) / 10 + 52428800;
        if (Storage.FreeSpace < num)
        {
            throw new InvalidOperationException(
                $"Not enough free space to convert world. {num / 1024 / 1024}MB required.");
        }

        using (var terrainSerializer = new TerrainSerializer22(null!, directoryName))
        {
            using (var terrainSerializer2 = new TerrainSerializer23(directoryName, ".new"))
            {
                foreach (var chunk2 in terrainSerializer.ChunkOffsets.Keys)
                {
                    var chunk = new TerrainChunk(null!, chunk2.X, chunk2.Y);
                    terrainSerializer.LoadChunk(chunk);
                    terrainSerializer2.SaveChunk(chunk);
                }
            }
        }

        Storage.MoveFile(Storage.CombinePaths(directoryName, "Chunks32h.dat"),
            Storage.CombinePaths(directoryName, "Chunks32h.dat.old"));
    }
}
