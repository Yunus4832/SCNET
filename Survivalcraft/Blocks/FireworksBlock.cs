using System.Globalization;

using Engine.Graphics;

namespace Game.Blocks;

public class FireworksBlock : Block
{
    public enum Shape
    {
        SmallBurst,
        LargeBurst,
        Circle,
        Disc,
        Ball,
        ShortTrails,
        LongTrails,
        FlatTrails
    }

    public const int Index = 215;

    public static readonly string[] HeadNames =
    [
        "HeadConeSmall",
        "HeadConeLarge",
        "HeadCylinderSmall",
        "HeadCylinderLarge",
        "HeadSphere",
        "HeadDiamondSmall",
        "HeadDiamondLarge",
        "HeadCylinderFlat"
    ];

    public static readonly Color[] FireworksColors =
    [
        new(255, 255, 255),
        new(85, 255, 255),
        new(255, 85, 85),
        new(85, 85, 255),
        new(255, 255, 85),
        new(85, 255, 85),
        new(255, 170, 0),
        new(255, 85, 255)
    ];

    public BlockMesh[] BodyBlockMeshes = new BlockMesh[2];

    public BlockMesh[] FinsBlockMeshes = new BlockMesh[2];

    public BlockMesh[] HeadBlockMeshes = new BlockMesh[64];

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Fireworks");
        var bodyMesh = model.FindMesh("Body")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            bodyMesh.ParentBone ??
            throw new InvalidOperationException("Required BodyMesh.ParentBone is null")
        );
        var finsMesh = model.FindMesh("Fins")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            finsMesh.ParentBone ??
            throw new InvalidOperationException("Required FinsMesh.ParentBone is null")
        );
        for (var i = 0; i < 64; i++)
        {
            var num = i / 8;
            var num2 = i % 8;
            var color = FireworksColors[num2];
            color *= 0.75f;
            color.A = byte.MaxValue;
            var headNameMesh = model.FindMesh(HeadNames[num])!;
            var boneAbsoluteTransform3 = BlockMesh.GetBoneAbsoluteTransform(
                headNameMesh.ParentBone ??
                throw new InvalidOperationException("Required HeadNameMesh.ParentBone is null"));
            HeadBlockMeshes[i] = new BlockMesh();
            HeadBlockMeshes[i].AppendModelMeshPart(headNameMesh.MeshParts[0],
                boneAbsoluteTransform3 * Matrix.CreateTranslation(0f, -0.25f, 0f), false, false, false, false, color);
        }

        for (var j = 0; j < 2; j++)
        {
            var num3 = 0.5f + j * 0.5f;
            var m = Matrix.CreateScale(new Vector3(num3, 1f, num3));
            BodyBlockMeshes[j] = new BlockMesh();
            BodyBlockMeshes[j].AppendModelMeshPart(bodyMesh.MeshParts[0],
                boneAbsoluteTransform * m * Matrix.CreateTranslation(0f, -0.25f, 0f), false, false, false, false,
                Color.White);
        }

        for (var k = 0; k < 2; k++)
        {
            FinsBlockMeshes[k] = new BlockMesh();
            FinsBlockMeshes[k].AppendModelMeshPart(finsMesh.MeshParts[0],
                boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.25f, 0f), false, false, false, false,
                k == 0 ? Color.White : new Color(224, 0, 0));
        }

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
        var data = Terrain.ExtractData(value);
        var color2 = GetColor(data);
        var shape = GetShape(data);
        var altitude = GetAltitude(data);
        var flickering = GetFlickering(data);
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            HeadBlockMeshes[(int)shape * 8 + color2],
            color,
            2f * size,
            ref matrix,
            environmentData
        );
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            BodyBlockMeshes[altitude],
            color,
            2f * size,
            ref matrix,
            environmentData
        );
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            FinsBlockMeshes[flickering ? 1 : 0],
            color,
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var color = GetColor(data);
        var shape = GetShape(data);
        var altitude = GetAltitude(data);
        var flickering = GetFlickering(data);
        return string.Format(LanguageManager.GetFireworks("Other", "1"),
            LanguageManager.GetFireworks("FireworksColorDisplayNames", color.ToString()),
            flickering ? LanguageManager.GetFireworks("Other", "2") : null,
            LanguageManager.GetFireworks("ShapeDisplayNames", ((int)shape).ToString()),
            altitude == 0 ? LanguageManager.GetFireworks("Other", "3") : LanguageManager.GetFireworks("Other", "4"));
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        var color = 0;
        while (color < 8)
        {
            int num;
            for (var altitude = 0; altitude < 2; altitude = num)
            {
                for (var flickering = 0; flickering < 2; flickering = num)
                {
                    for (var shape = 0; shape < 8; shape = num)
                    {
                        yield return Terrain.MakeBlockValue(215, 0,
                            SetColor(SetAltitude(SetShape(SetFlickering(0, flickering != 0), (Shape)shape), altitude),
                                color));
                        num = shape + 1;
                    }

                    num = flickering + 1;
                }

                num = altitude + 1;
            }

            num = color + 1;
            color = num;
        }
    }

    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        var shape = 0;
        while (shape < 8)
        {
            int num;
            for (var altitude = 0; altitude < 2; altitude = num)
            {
                for (var flickering = 0; flickering < 2; flickering = num)
                {
                    for (var color = 0; color < 8; color = num)
                    {
                        var craftingRecipe = new CraftingRecipe
                        {
                            ResultCount = 20,
                            ResultValue = Terrain.MakeBlockValue(215, 0,
                                SetColor(
                                    SetAltitude(SetShape(SetFlickering(0, flickering != 0), (Shape)shape), altitude),
                                    color)),
                            RemainsCount = 1,
                            RemainsValue = Terrain.MakeBlockValue(90),
                            RequiredHeatLevel = 0f,
                            Description = "制作烟花"
                        };

                        switch (shape)
                        {
                            case 0:
                                craftingRecipe.Ingredients[0] = string.Empty;
                                craftingRecipe.Ingredients[1] = "sulphurchunk";
                                craftingRecipe.Ingredients[2] = string.Empty;
                                break;
                            case 1:
                                craftingRecipe.Ingredients[0] = "sulphurchunk";
                                craftingRecipe.Ingredients[1] = "coalchunk";
                                craftingRecipe.Ingredients[2] = "sulphurchunk";
                                break;
                            case 2:
                                craftingRecipe.Ingredients[0] = "sulphurchunk";
                                craftingRecipe.Ingredients[1] = string.Empty;
                                craftingRecipe.Ingredients[2] = "sulphurchunk";
                                break;
                            case 3:
                                craftingRecipe.Ingredients[0] = "sulphurchunk";
                                craftingRecipe.Ingredients[1] = "sulphurchunk";
                                craftingRecipe.Ingredients[2] = "sulphurchunk";
                                break;
                            case 4:
                                craftingRecipe.Ingredients[0] = "coalchunk";
                                craftingRecipe.Ingredients[1] = "coalchunk";
                                craftingRecipe.Ingredients[2] = "coalchunk";
                                break;
                            case 5:
                                craftingRecipe.Ingredients[0] = string.Empty;
                                craftingRecipe.Ingredients[1] = "saltpeterchunk";
                                craftingRecipe.Ingredients[2] = string.Empty;
                                break;
                            case 6:
                                craftingRecipe.Ingredients[0] = "sulphurchunk";
                                craftingRecipe.Ingredients[1] = "saltpeterchunk";
                                craftingRecipe.Ingredients[2] = "sulphurchunk";
                                break;
                            case 7:
                                craftingRecipe.Ingredients[0] = "coalchunk";
                                craftingRecipe.Ingredients[1] = "saltpeterchunk";
                                craftingRecipe.Ingredients[2] = "coalchunk";
                                break;
                        }

                        switch (flickering)
                        {
                            case 0:
                                craftingRecipe.Ingredients[3] = "canvas";
                                craftingRecipe.Ingredients[5] = "canvas";
                                break;
                            case 1:
                                craftingRecipe.Ingredients[3] = "gunpowder";
                                craftingRecipe.Ingredients[5] = "gunpowder";
                                break;
                        }

                        switch (altitude)
                        {
                            case 0:
                                craftingRecipe.Ingredients[6] = "gunpowder";
                                craftingRecipe.Ingredients[7] = string.Empty;
                                craftingRecipe.Ingredients[8] = "gunpowder";
                                break;
                            case 1:
                                craftingRecipe.Ingredients[6] = "gunpowder";
                                craftingRecipe.Ingredients[7] = "gunpowder";
                                craftingRecipe.Ingredients[8] = "gunpowder";
                                break;
                        }

                        craftingRecipe.Ingredients[4] = "paintbucket:" +
                                                        (color != 7 ? color : 10)
                                                        .ToString(CultureInfo.InvariantCulture);
                        yield return craftingRecipe;
                        num = color + 1;
                    }

                    num = flickering + 1;
                }

                num = altitude + 1;
            }

            num = shape + 1;
            shape = num;
        }
    }

    public static Shape GetShape(int data)
    {
        return (Shape)(data & 7);
    }

    public static int SetShape(int data, Shape shape)
    {
        return (data & -8) | (int)(shape & Shape.FlatTrails);
    }

    public static int GetAltitude(int data)
    {
        return (data >> 3) & 1;
    }

    public static int SetAltitude(int data, int altitude)
    {
        return (data & -9) | ((altitude & 1) << 3);
    }

    public static bool GetFlickering(int data)
    {
        return ((data >> 4) & 1) != 0;
    }

    public static int SetFlickering(int data, bool flickering)
    {
        return (data & -17) | ((flickering ? 1 : 0) << 4);
    }

    public static int GetColor(int data)
    {
        return (data >> 5) & 7;
    }

    public static int SetColor(int data, int color)
    {
        return (data & -225) | ((color & 7) << 5);
    }
}
