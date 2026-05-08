namespace Game.Blocks;

public abstract class MountedElectricElementBlock : Block, IElectricElementBlock
{
    public abstract ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    );

    public abstract ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    );

    public virtual int GetConnectionMask(int value) => int.MaxValue;

    public abstract int GetFace(int value);

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = true;
        var block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
        return ((MountedElectricElementBlock)block).GetFace(value) == pistonFace;
    }
}
