using Engine.Graphics;

namespace Game.Blocks;

public class MusketBlock : Block
{
    public enum LoadState
    {
        Empty = 0,

        Gunpowder = 1,

        Wad = 2,

        Loaded = 3
    }

    public BlockMesh StandaloneBlockMeshLoaded = new();

    public BlockMesh StandaloneBlockMeshUnloaded = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Musket");
        var musketNode = model.FindNode("Musket")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(musketNode);
        var hammerNode = model.FindNode("Hammer")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(hammerNode);
        StandaloneBlockMeshUnloaded = new BlockMesh();
        StandaloneBlockMeshUnloaded.AppendModelPrimitive(
            musketNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform,
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMeshUnloaded.AppendModelPrimitive(
            hammerNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2,
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMeshLoaded = new BlockMesh();
        StandaloneBlockMeshLoaded.AppendModelPrimitive(
            musketNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform,
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMeshLoaded.AppendModelPrimitive(
            hammerNode.RequireMesh().Primitives[0],
            Matrix.CreateRotationX(0.7f) * nodeAbsoluteTransform2,
            false,
            false,
            false,
            false,
            Color.White
        );
        base.Initialize();
    }

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
    }

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
        if (GetHammerState(Terrain.ExtractData(value)))
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneBlockMeshLoaded,
                color,
                2f * size,
                ref matrix,
                environmentData
            );
        }
        else
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneBlockMeshUnloaded,
                color,
                2f * size,
                ref matrix,
                environmentData
            );
        }
    }

    public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
    {
        if (Terrain.ExtractContents(oldValue) != BlockIndex)
        {
            return true;
        }

        var data = Terrain.ExtractData(oldValue);
        return SetHammerState(Terrain.ExtractData(newValue), true) != SetHammerState(data, true);
    }

    public override int GetDamage(int value)
    {
        return (Terrain.ExtractData(value) >> 8) & 0xFF;
    }

    public override int SetDamage(int value, int damage)
    {
        var num = Terrain.ExtractData(value);
        num &= -65281;
        num |= MathUtils.Clamp(damage, 0, 255) << 8;
        return Terrain.ReplaceData(value, num);
    }

    public static LoadState GetLoadState(int data)
    {
        return (LoadState)(data & 3);
    }

    public static int SetLoadState(int data, LoadState loadState)
    {
        return (data & -4) | (int)(loadState & LoadState.Loaded);
    }

    public static bool GetHammerState(int data)
    {
        return (data & 4) != 0;
    }

    public static int SetHammerState(int data, bool state)
    {
        return (data & -5) | ((state ? 1 : 0) << 2);
    }

    public static BulletBlock.BulletType? GetBulletType(int data)
    {
        var num = (data >> 4) & 0xF;
        if (num != 0)
        {
            return (BulletBlock.BulletType)(num - 1);
        }

        return null;
    }

    public static int SetBulletType(int data, BulletBlock.BulletType? bulletType)
    {
        var num = (int)(bulletType.HasValue ? bulletType.Value + 1 : BulletBlock.BulletType.MusketBall);
        return (data & -241) | ((num & 0xF) << 4);
    }
}
