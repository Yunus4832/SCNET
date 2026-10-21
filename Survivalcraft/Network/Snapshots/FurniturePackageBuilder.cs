namespace Game.Network.Snapshots;

public static class FurniturePackageBuilder
{
    public static FurniturePackage Capture(FurnitureSet set) => new()
    {
        PackageEventType = FurniturePackage.EventType.NewFurnitureSet, AddXml = set.Name, FromName = set.ImportedFrom
    };

    public static FurniturePackage Capture(FurnitureSet set, int move) => new()
        { PackageEventType = FurniturePackage.EventType.MoveFurnitureSet, AddXml = set.Name, FurnitureIndex = move };

    public static FurniturePackage Capture(FurnitureDesign design, FurnitureSet set) => new()
    {
        PackageEventType = FurniturePackage.EventType.AddToFurnitureSet, AddXml = set.Name,
        FurnitureIndex = design.Index
    };

    public static FurniturePackage Capture(FurnitureDesign design) => new()
    {
        PackageEventType = FurniturePackage.EventType.DesignChain,
        AddXml = FurnitureSnapshotSerializer.SerializeDesigns(design.ListChain())
    };

    public static FurniturePackage Capture(List<FurnitureDesign> designs, string name) => new()
    {
        PackageEventType = FurniturePackage.EventType.ImportFurnitureSet,
        AddXml = FurnitureSnapshotSerializer.SerializeDesigns(designs), FromName = name
    };

    public static FurniturePackage Capture(
        FurnitureDesign design,
        IReadOnlyDictionary<Point3, int> cells,
        CellFace face,
        int value,
        bool isRequest = false
    ) => new()
    {
        PackageEventType = isRequest ? FurniturePackage.EventType.RequestAdd : FurniturePackage.EventType.Add,
        FurnitureIndex = design.Index,
        AddXml = CommonLib.SerializeVDict(design.Save()),
        PointDict = new Dictionary<Point3, int>(cells),
        CellFace = face,
        StartValue = value
    };
}
