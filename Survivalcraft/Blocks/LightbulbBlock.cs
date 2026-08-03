using Engine.Graphics;

namespace Game.Blocks;

public class LightbulbBlock : MountedElectricElementBlock, IPaintableBlock
{
    public const int Index = 139;

    public const string TypeName = nameof(LightbulbBlock);

    public readonly BlockMesh[] BulbBlockMeshes = new BlockMesh[6];

    public readonly BlockMesh[] BulbBlockMeshesLit = new BlockMesh[6];

    public readonly BoundingBox[][] CollisionBoxes = new BoundingBox[6][];

    public Color CopperColor = new(118, 56, 32);

    public readonly BlockMesh[] SidesBlockMeshes = new BlockMesh[6];

    public readonly BlockMesh StandaloneBulbBlockMesh = new();

    public readonly BlockMesh StandaloneSidesBlockMesh = new();

    public int? GetPaintColor(int value)
    {
        return GetColor(Terrain.ExtractData(value));
    }

    public int Paint(SubsystemTerrain? subsystemTerrain, int value, int? color)
    {
        var data = Terrain.ExtractData(value);
        return Terrain.ReplaceData(value, SetColor(data, color));
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Lightbulbs");
        var topMesh = model.FindMesh("Top")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            topMesh.ParentBone ??
            throw new InvalidOperationException("Required TopMesh.ParentBone is null")
        );
        var sidesMesh = model.FindMesh("Sides")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            sidesMesh.ParentBone ??
            throw new InvalidOperationException("Required SidesMesh.ParentBone is null")
        );
        for (var i = 0; i < 6; i++)
        {
            var m = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) * Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            BulbBlockMeshes[i] = new BlockMesh();
            BulbBlockMeshes[i].AppendModelMeshPart(topMesh.MeshParts[0], boneAbsoluteTransform * m,
                false, false, false, false, Color.White);
            BulbBlockMeshes[i].TransformTextureCoordinates(Matrix.CreateTranslation(0.1875f, 0.25f, 0f));
            BulbBlockMeshesLit[i] = new BlockMesh();
            BulbBlockMeshesLit[i].AppendModelMeshPart(topMesh.MeshParts[0], boneAbsoluteTransform * m,
                true, false, false, false, new Color(255, 255, 230));
            BulbBlockMeshesLit[i].TransformTextureCoordinates(Matrix.CreateTranslation(0.9375f, 0f, 0f));
            SidesBlockMeshes[i] = new BlockMesh();
            SidesBlockMeshes[i].AppendModelMeshPart(sidesMesh.MeshParts[0], boneAbsoluteTransform2 * m,
                false, false, true, false, Color.White);
            SidesBlockMeshes[i].TransformTextureCoordinates(Matrix.CreateTranslation(0.9375f, 0.1875f, 0f));
            CollisionBoxes[i] = [SidesBlockMeshes[i].CalculateBoundingBox()];
        }

        var m2 = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBulbBlockMesh.AppendModelMeshPart(topMesh.MeshParts[0], boneAbsoluteTransform * m2,
            false, false, true, false, Color.White);
        StandaloneBulbBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.1875f, 0.25f, 0f));
        StandaloneSidesBlockMesh.AppendModelMeshPart(sidesMesh.MeshParts[0],
            boneAbsoluteTransform2 * m2, false, false, true, false, Color.White);
        StandaloneSidesBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.9375f, 0.1875f, 0f));
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(139);
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(139, 0, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var color = GetColor(Terrain.ExtractData(value));
        return SubsystemPalette.GetName(color, LanguageManager.Get(TypeName, 1));
    }

    public override string GetCategory(int value)
    {
        return !GetColor(Terrain.ExtractData(value)).HasValue ? base.GetCategory(value) : "Painted";
    }

    public override int GetFace(int value)
    {
        return GetMountingFace(Terrain.ExtractData(value));
    }

    public override int GetEmittedLightAmount(int value)
    {
        return GetLightIntensity(Terrain.ExtractData(value));
    }

    public override int GetShadowStrength(int value)
    {
        var lightIntensity = GetLightIntensity(Terrain.ExtractData(value));
        return ShadowStrength - 10 * lightIntensity;
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result = default;
        result.Value = Terrain.MakeBlockValue(139, 0,
            SetMountingFace(Terrain.ExtractData(value), raycastResult.CellFace.Face));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        var color = GetColor(Terrain.ExtractData(oldValue));
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(139, 0, SetColor(0, color)),
            Count = 1
        });
        showDebris = true;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var mountingFace = GetMountingFace(Terrain.ExtractData(value));
        return mountingFace >= CollisionBoxes.Length ? [] : CollisionBoxes[mountingFace];
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
        var data = Terrain.ExtractData(value);
        var mountingFace = GetMountingFace(data);
        var lightIntensity = GetLightIntensity(data);
        var color = GetColor(data);
        var color2 = color.HasValue ? SubsystemPalette.GetColor(generator, color) : CopperColor;
        if (mountingFace < BulbBlockMeshes.Length)
        {
            if (lightIntensity <= 0)
            {
                generator.GenerateMeshVertices(this, x, y, z, BulbBlockMeshes[mountingFace], Color.White, null,
                    geometry.SubsetAlphaTest);
            }
            else
            {
                var r = (byte)(195 + lightIntensity * 4);
                var g = (byte)(180 + lightIntensity * 5);
                var b = (byte)(165 + lightIntensity * 6);
                generator.GenerateMeshVertices(this, x, y, z, BulbBlockMeshesLit[mountingFace], new Color(r, g, b),
                    null, geometry.SubsetOpaque);
            }

            generator.GenerateMeshVertices(this, x, y, z, SidesBlockMeshes[mountingFace], color2, null,
                geometry.SubsetOpaque);
            generator.GenerateWireVertices(value, x, y, z, mountingFace, 0.875f, Vector2.Zero, geometry.SubsetOpaque);
        }
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
        var color2 = GetColor(Terrain.ExtractData(value));
        var c = color2.HasValue ? SubsystemPalette.GetColor(environmentData, color2) : CopperColor;
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneSidesBlockMesh,
            color * c,
            2f * size,
            ref matrix,
            environmentData
        );
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBulbBlockMesh,
            color,
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new LightBulbElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)), value);
    }

    public override ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        var face2 = GetFace(value);
        if (face == face2 && SubsystemElectricity.GetConnectorDirection(face2, 0, connectorFace).HasValue)
        {
            return ElectricConnectorType.Input;
        }

        return null;
    }

    public static int GetMountingFace(int data)
    {
        return data & 7;
    }

    public static int SetMountingFace(int data, int face)
    {
        return (data & -8) | (face & 7);
    }

    public static int GetLightIntensity(int data)
    {
        return (data >> 3) & 0xF;
    }

    public static int SetLightIntensity(int data, int intensity)
    {
        return (data & -121) | ((intensity & 0xF) << 3);
    }

    public static int? GetColor(int data)
    {
        if ((data & 0x80) != 0)
        {
            return (data >> 8) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -3969) | 0x80 | ((color.Value & 0xF) << 8);
        }

        return data & -3969;
    }
}
