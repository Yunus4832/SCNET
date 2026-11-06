using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class EggBlockPresentation(EggBlock block, BlockPresentationCatalog catalog) : BlockPresentation(block, catalog)
{
    protected new EggBlock Block => (EggBlock)base.Block;

    private readonly Dictionary<int, BlockMesh> _eggMeshes = new();

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var eggType = Block.GetEggType(Terrain.ExtractData(value));
        var data = Terrain.ExtractData(value);
        var isCooked = EggBlock.GetIsCooked(data);
        var isLaid = EggBlock.GetIsLaid(data);
        if (isCooked)
        {
            return Localization.ClientLanguage.Catalog.Get(EggBlock.TypeName, 1) + GetEggDisplayName(eggType);
        }

        if (!isLaid)
        {
            return GetEggDisplayName(eggType);
        }

        return Localization.ClientLanguage.Catalog.Get(EggBlock.TypeName, 2) + GetEggDisplayName(eggType);
    }

    public override string GetCategory(int value)
    {
        return "Spawner Eggs";
    }

    public override string GetDescription(int value)
    {
        var eggType = Block.GetEggType(Terrain.ExtractData(value));
        var displayName = GetEggDisplayName(Block.EggTypes[eggType.EggTypeIndex]);
        return Localization.ClientLanguage.Catalog.Get(EggBlock.TypeName, 3) + displayName[..^1];
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
        var eggType = Block.GetEggType(data);
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            _eggMeshes[eggType.EggTypeIndex],
            color,
            eggType.Scale * size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        _eggMeshes.Clear();
        var model = ContentManager.Get<Model>("Models/Egg");
        var eggNode = model.FindNode("Egg")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(eggNode);
        foreach (var eggType in Block.ReadOnlyEggTypes.OfType<EggType>())
        {
            var mesh = new BlockMesh();
            _eggMeshes.Add(eggType.EggTypeIndex, mesh);
            mesh.AppendModelPrimitive(
                eggNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform,
                false,
                false,
                false,
                false,
                eggType.Color
            );
            var identity = Matrix.Identity;
            if (eggType.SwapUv)
            {
                identity.M11 = 0f;
                identity.M12 = 1f;
                identity.M21 = 1f;
                identity.M22 = 0f;
            }

            identity *= Matrix.CreateScale(0.0625f * eggType.ScaleUv.X, 0.0625f * eggType.ScaleUv.Y, 1f);
            identity *= Matrix.CreateTranslation(
                eggType.TextureSlot % 16 / 16f,
                eggType.TextureSlot / 16 / 16f,
                0f
            );
            mesh.TransformTextureCoordinates(identity);
        }


        base.Initialize();
    }

    private static string GetEggDisplayName(EggType data)
    {
        return data.DisplayNameKey is null
            ? data.DisplayName
            : Localization.ClientLanguage.Catalog.GetDatabase("DisplayName", data.DisplayNameKey);
    }
}
