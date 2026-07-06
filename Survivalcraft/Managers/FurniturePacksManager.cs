using System.IO.Compression;
using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

namespace Game.Managers;

public static class FurniturePacksManager
{
    private static readonly List<string> _furniturePackNames = [];

    public static ReadOnlyList<string> ReadOnlyFurniturePackNames => new(_furniturePackNames);

    public static event Action<string>? FurniturePackDeleted;

    public static void Initialize()
    {
        Storage.CreateDirectory(GamePaths.FurniturePacks);
    }

    public static string GetFileName(string name)
    {
        return Storage.CombinePaths(GamePaths.FurniturePacks, name);
    }

    public static string GetDisplayName(string name)
    {
        return Storage.GetFileNameWithoutExtension(name);
    }

    public static DateTime GetCreationDate(string name)
    {
        try
        {
            return Storage.GetFileLastWriteTime(GetFileName(name));
        }
        catch
        {
            return new DateTime(2000, 1, 1);
        }
    }

    public static string ImportFurniturePack(string name, Stream stream)
    {
        ValidateFurniturePack(stream);
        stream.Position = 0L;
        var fileNameWithoutExtension = Storage.GetFileNameWithoutExtension(name);
        name = fileNameWithoutExtension + ".scfpack";
        var fileName = GetFileName(name);
        var num = 0;
        while (Storage.FileExists(fileName))
        {
            num++;
            if (num > 9)
            {
                throw new InvalidOperationException("Duplicate name. Delete existing content with conflicting names.");
            }

            name = $"{fileNameWithoutExtension} ({num}).scfpack";
            fileName = GetFileName(name);
        }

        using var destination = Storage.OpenFile(fileName, OpenFileMode.Create);
        stream.CopyTo(destination);
        return name;
    }

    public static void ExportFurniturePack(string name, Stream stream)
    {
        using var stream2 = Storage.OpenFile(GetFileName(name), OpenFileMode.Read);
        stream2.CopyTo(stream);
    }

    public static string CreateFurniturePack(string name, ICollection<FurnitureDesign?> designs)
    {
        var memoryStream = new MemoryStream();
        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var valuesDictionary = new ValuesDictionary();
            SubsystemFurnitureBlockBehavior.SaveFurnitureDesigns(valuesDictionary, designs);
            var xElement = new XElement("FurnitureDesigns");
            valuesDictionary.Save(xElement);
            var entry = zipArchive.CreateEntry("FurnitureDesigns.xml", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            xElement.Save(entryStream);
        }

        memoryStream.Position = 0L;
        return ImportFurniturePack(name, memoryStream);
    }

    public static void DeleteFurniturePack(string name)
    {
        try
        {
            Storage.DeleteFile(GetFileName(name));
            FurniturePackDeleted?.Invoke(name);
        }
        catch (Exception e)
        {
            ExceptionManager.ReportExceptionToUser($"Unable to delete furniture pack \"{name}\"", e);
        }
    }

    public static void UpdateFurniturePacksList()
    {
        _furniturePackNames.Clear();
        foreach (var item in Storage.ListFileNames(GamePaths.FurniturePacks))
        {
            if (Storage.GetExtension(item).ToLower() == ".scfpack")
            {
                _furniturePackNames.Add(item);
            }
        }
    }

    public static List<FurnitureDesign> LoadFurniturePack(SubsystemTerrain? subsystemTerrain, string name)
    {
        using var stream = Storage.OpenFile(GetFileName(name), OpenFileMode.Read);
        return LoadFurniturePack(subsystemTerrain, stream);
    }

    private static void ValidateFurniturePack(Stream stream)
    {
        LoadFurniturePack(null, stream);
    }

    private static List<FurnitureDesign> LoadFurniturePack(SubsystemTerrain? subsystemTerrain, Stream stream)
    {
        using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read, true);
        var entries = zipArchive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToList();
        if (entries.Count != 1 || entries[0].FullName.Replace('\\', '/') != "FurnitureDesigns.xml")
        {
            throw new InvalidOperationException("Invalid furniture pack.");
        }

        var memoryStream = new MemoryStream();
        using (var entryStream = entries[0].Open())
        {
            entryStream.CopyTo(memoryStream);
        }

        memoryStream.Position = 0L;
        var overridesNode = XElement.Load(memoryStream);
        var valuesDictionary = new ValuesDictionary();
        valuesDictionary.ApplyOverrides(overridesNode);
        return SubsystemFurnitureBlockBehavior.LoadFurnitureDesigns(subsystemTerrain, valuesDictionary);
    }
}
