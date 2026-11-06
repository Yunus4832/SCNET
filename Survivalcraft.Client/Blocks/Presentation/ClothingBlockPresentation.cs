using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

namespace Game.Blocks.Presentation;

public class ClothingBlockPresentation(ClothingBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new ClothingBlock Block => (ClothingBlock)base.Block;

    private readonly Dictionary<int, Texture2D> _clothingTextures = new();

    private BlockMesh _innerMesh = null!;

    private BlockMesh _outerMesh = null!;

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var clothingData = Block.GetClothingData(data);
        var clothingColor = ClothingBlock.GetClothingColor(data);
        var displayName = GetClothingDisplayName(clothingData);
        return clothingColor != 0 ? PalettePresentation.GetName(clothingColor, displayName) : displayName;
    }

    public override string GetDescription(int value)
    {
        var data = Terrain.ExtractData(value);
        var clothingData = Block.GetClothingData(data);
        return Localization.ClientLanguage.Catalog.TryGetBlock(
            $"ClothingBlock:{clothingData.Index}",
            "Description",
            out var text
        )
            ? text!
            : clothingData.Description;
    }

    public override string GetCategory(int value)
    {
        return ClothingBlock.GetClothingColor(Terrain.ExtractData(value)) == 0 ? base.GetCategory(value) : "Dyed";
    }

    public override int GetDisplayOrder(int value)
    {
        return Block.GetClothingData(Terrain.ExtractData(value)).DisplayIndex;
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
        var clothingColor = ClothingBlock.GetClothingColor(data);
        var clothingData = Block.GetClothingData(data);
        var matrix2 = _slotTransforms[(int)clothingData.Slot] * Matrix.CreateScale(size) * matrix;
        if (clothingData.IsOuter)
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                _outerMesh,
                GetClothingTexture(clothingData.Index),
                color * PalettePresentation.GetFabricColor(environmentData.SubsystemTerrain, clothingColor),
                1f,
                ref matrix2,
                environmentData
            );
        }
        else
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                _innerMesh,
                GetClothingTexture(clothingData.Index),
                color * PalettePresentation.GetFabricColor(environmentData.SubsystemTerrain, clothingColor),
                1f,
                ref matrix2,
                environmentData
            );
        }
    }

    public Texture2D GetClothingTexture(int index) => _clothingTextures[index];


    public override void Initialize()
    {
        foreach (var definition in Block.ClothingDefinitions)
        {
            _clothingTextures.Add(definition.Index, ContentManager.Get<Texture2D>(definition.TextureName));
        }

        var playerModel = CharacterSkinsManager.GetCharacterModel(CharacterSex.Male);
        var array = new Matrix[playerModel.Nodes.Count];
        playerModel.CopyAbsoluteNodeTransformsTo(array);
        var index = playerModel.FindNode("Hand1")!.Index;
        var index2 = playerModel.FindNode("Hand2")!.Index;
        array[index] = Matrix.CreateRotationY(0.1f) * array[index];
        array[index2] = Matrix.CreateRotationY(-0.1f) * array[index2];
        _innerMesh = new BlockMesh();
        foreach (var node in playerModel.MeshNodes)
        {
            var matrix = array[node.Index];
            foreach (var meshPart in node.RequireMesh().Primitives)
            {
                var color = Color.White * 0.8f;
                color.A = byte.MaxValue;
                _innerMesh.AppendModelPrimitive(meshPart, matrix, false, false, false, false, Color.White);
                _innerMesh.AppendModelPrimitive(meshPart, matrix, false, true, false, true, color);
            }
        }

        var outerClothingModel = CharacterSkinsManager.GetOuterClothingModel(CharacterSex.Male);
        var array2 = new Matrix[outerClothingModel.Nodes.Count];
        outerClothingModel.CopyAbsoluteNodeTransformsTo(array2);
        var index3 = outerClothingModel.FindNode("Leg1")!.Index;
        var index4 = outerClothingModel.FindNode("Leg2")!.Index;
        array2[index3] = Matrix.CreateTranslation(-0.02f, 0f, 0f) * array2[index3];
        array2[index4] = Matrix.CreateTranslation(0.02f, 0f, 0f) * array2[index4];
        _outerMesh = new BlockMesh();
        foreach (var node2 in outerClothingModel.MeshNodes)
        {
            var matrix2 = array2[node2.Index];
            foreach (var meshPart2 in node2.RequireMesh().Primitives)
            {
                var color2 = Color.White * 0.8f;
                color2.A = byte.MaxValue;
                _outerMesh.AppendModelPrimitive(meshPart2, matrix2, false, false, false, false, Color.White);
                _outerMesh.AppendModelPrimitive(meshPart2, matrix2, false, true, false, true, color2);
            }
        }

        base.Initialize();
    }

    private static readonly Matrix[] _slotTransforms =
    [
        Matrix.CreateTranslation(0f, -1.5f, 0f) * Matrix.CreateScale(2.7f),
        Matrix.CreateTranslation(0f, -1.1f, 0f) * Matrix.CreateScale(2.7f),
        Matrix.CreateTranslation(0f, -0.5f, 0f) * Matrix.CreateScale(2.7f),
        Matrix.CreateTranslation(0f, -0.1f, 0f) * Matrix.CreateScale(2.7f)
    ];


    public static string GetClothingDisplayName(ClothingData data)
    {
        return Localization.ClientLanguage.Catalog.TryGetBlock(
            $"ClothingBlock:{data.Index}",
            "DisplayName",
            out var text
        )
            ? text!
            : data.DisplayName;
    }
}
