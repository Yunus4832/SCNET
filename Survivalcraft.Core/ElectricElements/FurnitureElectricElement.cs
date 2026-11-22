namespace Game.ElectricElements;

public abstract class FurnitureElectricElement(
    SubsystemElectricity subsystemElectricity,
    Point3 point
) : ElectricElement(subsystemElectricity, GetMountingCellFaces(subsystemElectricity, point))
{
    public static IEnumerable<CellFace> GetMountingCellFaces(SubsystemElectricity subsystemElectricity, Point3 point)
    {
        var data = Terrain.ExtractData(
            subsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z)
        );
        var rotation = FurnitureBlock.GetRotation(data);
        var designIndex = FurnitureBlock.GetDesignIndex(data);
        var design = subsystemElectricity.SubsystemTerrain.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        if (design == null)
        {
            yield break;
        }

        var face = 0;
        while (face < 6)
        {
            var designFace = face < 4 ? (face - rotation + 4) % 4 : face;
            if ((design.MountingFacesMask & (1 << designFace)) != 0)
            {
                yield return new CellFace(point.X, point.Y, point.Z, CellFace.OppositeFace(face));
            }

            var nextFace = face + 1;
            face = nextFace;
        }
    }
}
