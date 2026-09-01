using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

namespace Game.Managers;

public static class FurniturePacksManager
{
    private const string _assetExtension = ".xml";
    private static readonly List<string> _furniturePackNames = [];

    public static ReadOnlyList<string> ReadOnlyFurniturePackNames => new(_furniturePackNames);

    public static event Action<string>? FurniturePackDeleted;

    public static void Initialize()
    {
        Storage.CreateDirectory(GamePaths.FurniturePacks);
    }

    public static string GetFileName(string name)
    {
        return Storage.CombinePaths(GamePaths.FurniturePacks, name + _assetExtension);
    }

    public static string GetDisplayName(string name)
    {
        return ContentAssetStore.GetDisplayName(GamePaths.FurniturePacks, name, _assetExtension);
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
        return ContentAssetStore.Install(GamePaths.FurniturePacks, _assetExtension,
            Storage.GetFileNameWithoutExtension(name), stream, ValidateFurniturePack);
    }

    public static string ImportFurnitureDesigns(string name, Stream designs)
    {
        return ImportFurniturePack(name, designs);
    }

    public static string ReplaceFurnitureDesigns(string assetKey, string displayName, Stream designs) =>
        ContentAssetStore.Replace(GamePaths.FurniturePacks, _assetExtension, assetKey, displayName, designs,
            ValidateFurniturePack);

    public static void ExportFurniturePack(string name, Stream stream)
    {
        using var stream2 = Storage.OpenFile(GetFileName(name), OpenFileMode.Read);
        stream2.CopyTo(stream);
    }

    public static string CreateFurniturePack(string name, ICollection<FurnitureDesign?> designs)
    {
        var memoryStream = new MemoryStream();
        var valuesDictionary = new ValuesDictionary();
        SubsystemFurnitureBlockBehavior.SaveFurnitureDesigns(valuesDictionary, designs);
        var xElement = new XElement("FurnitureDesigns");
        valuesDictionary.Save(xElement);
        xElement.Save(memoryStream);

        memoryStream.Position = 0L;
        return ImportFurniturePack(name, memoryStream);
    }

    public static void DeleteFurniturePack(string name)
    {
        try
        {
            ContentAssetStore.Delete(GamePaths.FurniturePacks, name, _assetExtension);
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
            if (ContentAssetStore.IsComplete(GamePaths.FurniturePacks, item, _assetExtension))
            {
                _furniturePackNames.Add(Storage.GetFileNameWithoutExtension(item));
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
        var overridesNode = XElement.Load(stream);
        var valuesDictionary = new ValuesDictionary();
        valuesDictionary.ApplyOverrides(overridesNode);
        return SubsystemFurnitureBlockBehavior.LoadFurnitureDesigns(subsystemTerrain, valuesDictionary);
    }
}
