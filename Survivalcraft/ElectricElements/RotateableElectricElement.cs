namespace Game.ElectricElements;

public abstract class RotateableElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : MountedElectricElement(subsystemElectricity, cellFace)
{
    public int Rotation
    {
        get
        {
            var cellFace = CellFaces[0];
            return RotatableMountedElectricElementBlock.GetRotation(Terrain.ExtractData(
                SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z)));
        }
        set
        {
            var cellFace = CellFaces[0];
            var cellValue =
                SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
            var value2 = Terrain.ReplaceData(cellValue,
                RotatableMountedElectricElementBlock.SetRotation(Terrain.ExtractData(cellValue), value % 4));
            SubsystemElectricity.SubsystemTerrain.ChangeCell(cellFace.X, cellFace.Y, cellFace.Z, value2);
            SubsystemElectricity.SubsystemAudio.PlaySound("Audio/Click", 1f, 0f,
                new Vector3(cellFace.X, cellFace.Y, cellFace.Z), 2f, true);
        }
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        _ = ++Rotation;
        return true;
    }
}
