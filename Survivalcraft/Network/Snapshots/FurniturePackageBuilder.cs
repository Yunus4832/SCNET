using Game.Furniture;

namespace Game.Network.Snapshots;

public static class FurniturePackageBuilder
{
    public static FurniturePackage Capture(FurnitureSet set) => new FurnitureSetCreatedPackage
        { Name = set.Name, ImportedFrom = set.ImportedFrom };

    public static FurniturePackage Capture(FurnitureSet set, int move) =>
        new FurnitureSetMovePackage { SetName = set.Name, FurnitureIndex = move };

    public static FurniturePackage Capture(FurnitureDesign design, FurnitureSet set) =>
        new FurnitureSetAddDesignPackage { SetName = set.Name, FurnitureIndex = design.Index };

    public static FurniturePackage Capture(FurnitureDesign design) =>
        new FurnitureDesignChainPackage
            { DesignXml = FurnitureSnapshotSerializer.SerializeDesigns(design.ListChain()) };

    public static FurniturePackage Capture(List<FurnitureDesign> designs, string name) =>
        new FurnitureSetImportPackage
            { DesignXml = FurnitureSnapshotSerializer.SerializeDesigns(designs), Name = name };

    public static FurniturePackage Capture(
        FurnitureDesign design,
        IReadOnlyDictionary<Point3, int> cells,
        CellFace face,
        int value,
        bool isRequest = false
    ) => ((isRequest
        ? new FurnitureDesignAddRequestPackage
        {
            FurnitureIndex = design.Index, DesignXml = CommonLib.SerializeVDict(design.Save()),
            PointDict = new Dictionary<Point3, int>(cells), CellFace = face, StartValue = value
        }
        : new FurnitureDesignAddedPackage
            { FurnitureIndex = design.Index, DesignXml = CommonLib.SerializeVDict(design.Save()) }));
}
