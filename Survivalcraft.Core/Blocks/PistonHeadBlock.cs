namespace Game.Blocks;

public class PistonHeadBlock : Block
{
    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        var data = Terrain.ExtractData(value);
        return face != GetFace(data);
    }


    public static PistonMode GetMode(int data) => (PistonMode)(data & 3);

    public static int SetMode(int data, PistonMode mode) => (data & -4) | (int)(mode & (PistonMode)3);

    public static bool GetIsShaft(int data) => (data & 4) != 0;

    public static int SetIsShaft(int data, bool isShaft) => (data & -5) | (isShaft ? 4 : 0);

    public static int GetFace(int data) => (data >> 3) & 7;

    public static int SetFace(int data, int face) => (data & -57) | ((face & 7) << 3);

    public override bool IsCollapseSupportBlock(SubsystemTerrain subsystemTerrain, int value) => true;

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
    }

    public override bool IsFaceNonAttachable(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int attachBlockValue
    )
    {
        return IsFaceTransparent(subsystemTerrain, face, value);
    }
}
