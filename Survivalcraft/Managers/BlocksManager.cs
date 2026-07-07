using System.Xml.Linq;

using Engine.Graphics;
using Engine.Media;
using Engine.Serialization;

using Game.Modding.Blocks;

namespace Game.Managers;

public static class BlocksManager
{
    private static readonly List<string> _categories = [];

    private static readonly DrawBlockEnvironmentData _defaultEnvironmentData = new();

    private static readonly Vector4[] _slotTexCoords = new Vector4[256];

    private static readonly Dictionary<ImageExtrusionKey, BlockMesh> _imageExtrusionsCache = new();

    private const bool _drawImageExtrusionEnabled = true;

    private static readonly Dictionary<int, ResourceId> _blockIdsByIndex = [];

    private static readonly List<Block> _registeredBlocks = [];

    public static Block[] Blocks { get; private set; } = new Block[1024];

    public static FluidBlock?[] FluidBlocks { get; private set; } = new FluidBlock[1024];

    public static ReadOnlyList<string> ReadOnlyCategories => new(_categories);

    public static IReadOnlyList<Block> RegisteredBlocks => _registeredBlocks;

    public static void Initialize()
    {
        var runtime = CurrentModRuntime.Value
            ?? throw new InvalidOperationException("No active game mod runtime.");
        runtime.InitializeBlocks();
    }

    public static void Initialize(BlockRuntimeCatalog? catalog)
    {
        Initialize(catalog, null);
    }

    public static void Initialize(BlockRuntimeCatalog? catalog, XElement? clothingData)
    {
        if (catalog is null)
        {
            var runtime = CurrentModRuntime.Value
                ?? throw new InvalidOperationException("No active game mod runtime.");
            catalog = runtime.Blocks;
            clothingData ??= runtime.Data.BuildClothing();
        }

        if (clothingData is not null)
        {
            ClothingBlock.SetInitializationData(clothingData);
        }

        for (var i = 0; i < Blocks.Length; i++)
        {
            Blocks[i] = null!;
            FluidBlocks[i] = null;
        }

        _blockIdsByIndex.Clear();
        _registeredBlocks.Clear();

        _categories.Clear();
        _categories.Add("Terrain");
        _categories.Add("Minerals");
        _categories.Add("Plants");
        _categories.Add("Construction");
        _categories.Add("Items");
        _categories.Add("Tools");
        _categories.Add("Weapons");
        _categories.Add("Clothes");
        _categories.Add("Electrics");
        _categories.Add("Food");
        _categories.Add("Spawner Eggs");
        _categories.Add("Painted");
        _categories.Add("Dyed");
        _categories.Add("Fireworks");
        CalculateSlotTexCoordTables();
        int num;
        foreach (var block in catalog.ById.Values
                     .OrderBy(entry => entry.RuntimeIndex)
                     .Select(entry => entry.Block))
        {
            Blocks[block.BlockIndex] = block;
            _registeredBlocks.Add(block);
            if (block is FluidBlock fluidBlock)
            {
                FluidBlocks[fluidBlock.BlockIndex] = fluidBlock;
            }
        }

        if (catalog is not null)
        {
            foreach (var entry in catalog.ById.Values)
            {
                _blockIdsByIndex.Add(entry.RuntimeIndex, entry.Id);
            }
        }

        if (Blocks[0] is null)
        {
            throw new InvalidOperationException("Blocks[0] is null");
        }

        for (num = 0; num < Blocks.Length; num++)
        {
            if ((Block?)Blocks[num] is not null)
            {
                continue;
            }

            Blocks[num] = Blocks[0];
        }

        var runtimeCatalog = catalog ?? throw new InvalidOperationException("Block runtime catalog is not initialized.");
        foreach (var dataEntry in runtimeCatalog.DataEntries)
        {
            LoadBlocksData(dataEntry.Read());
        }

        foreach (var block in Blocks)
        {
            try
            {
                block.Initialize();
                if (string.IsNullOrEmpty(block.CraftingId))
                {
                    block.CraftingId = block.GetType().Name;
                }
            }
            catch (Exception e)
            {
                Log.Warning($"加载方块{block.GetType().FullName}错误:{e.Message}");
            }

            foreach (var value in block.GetCreativeValues())
            {
                var category = block.GetCategory(value);
                AddCategory(category);
            }
        }

        GameManager.ProjectDisposed += delegate { _imageExtrusionsCache.Clear(); };
    }

    public static IEnumerable<int> GetCreativeValues()
    {
        return _registeredBlocks
            .SelectMany(block => block.GetCreativeValues())
            .OrderBy(value => Blocks[Terrain.ExtractContents(value)].GetDisplayOrder(value));
    }

    public static bool TryGetBlockId(int runtimeIndex, out ResourceId id)
    {
        return _blockIdsByIndex.TryGetValue(runtimeIndex, out id);
    }

    public static void AddCategory(string category)
    {
        if (!_categories.Contains(category))
        {
            _categories.Add(category);
        }
    }

    public static Block? FindBlockByTypeName(string typeName, bool throwIfNotFound)
    {
        var block = Blocks.FirstOrDefault(b => b.GetType().Name == typeName);
        if (block == null && throwIfNotFound)
        {
            throw new InvalidOperationException(string.Format(LanguageManager.Get("BlocksManager", 1), typeName));
        }

        return block;
    }

    public static Block[] FindBlocksByCraftingId(string craftingId)
    {
        return Blocks.Where(c => c.MatchCraftingId(craftingId)).ToArray();
    }

    // 新增高度渲染cube
    public static void DrawCubeBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Vector3 size,
        float height,
        ref Matrix matrix,
        Color color,
        Color topColor,
        DrawBlockEnvironmentData? environmentData
    )
    {
        environmentData ??= _defaultEnvironmentData;
        var texture = environmentData.SubsystemTerrain is not null
            ? environmentData.SubsystemTerrain.SubsystemAnimatedTextures.AnimatedBlocksTexture
            : BlocksTexturesManager.DefaultBlocksTexture;
        var texturedBatch3D = primitivesRenderer.TexturedBatch(texture, true, 0, null,
            RasterizerState.CullCounterClockwiseScissor, BlendState.AlphaBlend, SamplerState.PointClamp);
        var s = LightingManager.LightIntensityByLightValue[environmentData.Light];
        color = Color.MultiplyColorOnlyNotSaturated(color, s);
        topColor = Color.MultiplyColorOnlyNotSaturated(topColor, s);
        var translation = matrix.Translation;
        var vector = matrix.Right * size.X;
        var vector2 = matrix.Up * size.Y * height;
        var vector3 = matrix.Forward * size.Z;
        var v = translation + 0.5f * (-vector - vector2 - vector3);
        var v2 = translation + 0.5f * (vector - vector2 - vector3);
        var v3 = translation + 0.5f * (-vector + vector2 - vector3);
        var v4 = translation + 0.5f * (vector + vector2 - vector3);
        var v5 = translation + 0.5f * (-vector - vector2 + vector3);
        var v6 = translation + 0.5f * (vector - vector2 + vector3);
        var v7 = translation + 0.5f * (-vector + vector2 + vector3);
        var v8 = translation + 0.5f * (vector + vector2 + vector3);
        if (environmentData.ViewProjectionMatrix.HasValue)
        {
            var m = environmentData.ViewProjectionMatrix.Value;
            Vector3.Transform(ref v, ref m, out v);
            Vector3.Transform(ref v2, ref m, out v2);
            Vector3.Transform(ref v3, ref m, out v3);
            Vector3.Transform(ref v4, ref m, out v4);
            Vector3.Transform(ref v5, ref m, out v5);
            Vector3.Transform(ref v6, ref m, out v6);
            Vector3.Transform(ref v7, ref m, out v7);
            Vector3.Transform(ref v8, ref m, out v8);
        }

        var num = Terrain.ExtractContents(value);
        var block = Blocks[num];
        var vector4 = _slotTexCoords[block.GetFaceTextureSlot(0, value)];
        vector4.W = MathUtils.Lerp(vector4.Y, vector4.W, height);
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnlyNotSaturated(color, LightingManager.CalculateLighting(-matrix.Forward)),
            p1: v, p2: v3, p3: v4, p4: v2, texCoord1: new Vector2(vector4.X, vector4.W),
            texCoord2: new Vector2(vector4.X, vector4.Y), texCoord3: new Vector2(vector4.Z, vector4.Y),
            texCoord4: new Vector2(vector4.Z, vector4.W));
        vector4 = _slotTexCoords[block.GetFaceTextureSlot(2, value)];
        vector4.W = MathUtils.Lerp(vector4.Y, vector4.W, height);
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnlyNotSaturated(color, LightingManager.CalculateLighting(matrix.Forward)),
            p1: v5, p2: v6, p3: v8, p4: v7, texCoord1: new Vector2(vector4.Z, vector4.W),
            texCoord2: new Vector2(vector4.X, vector4.W), texCoord3: new Vector2(vector4.X, vector4.Y),
            texCoord4: new Vector2(vector4.Z, vector4.Y));
        vector4 = _slotTexCoords[block.GetFaceTextureSlot(5, value)];
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnlyNotSaturated(color, LightingManager.CalculateLighting(-matrix.Up)), p1: v,
            p2: v2, p3: v6, p4: v5, texCoord1: new Vector2(vector4.X, vector4.Y),
            texCoord2: new Vector2(vector4.Z, vector4.Y), texCoord3: new Vector2(vector4.Z, vector4.W),
            texCoord4: new Vector2(vector4.X, vector4.W));
        vector4 = _slotTexCoords[block.GetFaceTextureSlot(4, value)];
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnlyNotSaturated(topColor, LightingManager.CalculateLighting(matrix.Up)), p1: v3,
            p2: v7, p3: v8, p4: v4, texCoord1: new Vector2(vector4.X, vector4.W),
            texCoord2: new Vector2(vector4.X, vector4.Y), texCoord3: new Vector2(vector4.Z, vector4.Y),
            texCoord4: new Vector2(vector4.Z, vector4.W));
        vector4 = _slotTexCoords[block.GetFaceTextureSlot(1, value)];
        vector4.W = MathUtils.Lerp(vector4.Y, vector4.W, height);
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnlyNotSaturated(color, LightingManager.CalculateLighting(-matrix.Right)), p1: v,
            p2: v5, p3: v7, p4: v3, texCoord1: new Vector2(vector4.Z, vector4.W),
            texCoord2: new Vector2(vector4.X, vector4.W), texCoord3: new Vector2(vector4.X, vector4.Y),
            texCoord4: new Vector2(vector4.Z, vector4.Y));
        vector4 = _slotTexCoords[block.GetFaceTextureSlot(3, value)];
        vector4.W = MathUtils.Lerp(vector4.Y, vector4.W, height);
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnlyNotSaturated(color, LightingManager.CalculateLighting(matrix.Right)), p1: v2,
            p2: v4, p3: v8, p4: v6, texCoord1: new Vector2(vector4.X, vector4.W),
            texCoord2: new Vector2(vector4.X, vector4.Y), texCoord3: new Vector2(vector4.Z, vector4.Y),
            texCoord4: new Vector2(vector4.Z, vector4.W));
    }


    public static void DrawCubeBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Vector3 size,
        ref Matrix matrix,
        Color color,
        Color topColor,
        DrawBlockEnvironmentData environmentData
    )
    {
        DrawCubeBlock(
            primitivesRenderer,
            value,
            size,
            ref matrix,
            color,
            topColor,
            environmentData,
            environmentData.SubsystemTerrain != null
                ? environmentData.SubsystemTerrain.SubsystemAnimatedTextures.AnimatedBlocksTexture
                : BlocksTexturesManager.DefaultBlocksTexture
        );
    }

    private static void DrawCubeBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Vector3 size,
        ref Matrix matrix,
        Color color,
        Color topColor,
        DrawBlockEnvironmentData? environmentData,
        Texture2D texture
    )
    {
        environmentData ??= _defaultEnvironmentData;
        var texturedBatch3D = primitivesRenderer.TexturedBatch(texture, true, 0, null,
            RasterizerState.CullCounterClockwiseScissor, null, SamplerState.PointClamp);
        var s = LightingManager.LightIntensityByLightValue[environmentData.Light];
        color = Color.MultiplyColorOnly(color, s);
        topColor = Color.MultiplyColorOnly(topColor, s);
        var translation = matrix.Translation;
        var vector = matrix.Right * size.X;
        var v = matrix.Up * size.Y;
        var v2 = matrix.Forward * size.Z;
        var v3 = translation + 0.5f * (-vector - v - v2);
        var v4 = translation + 0.5f * (vector - v - v2);
        var v5 = translation + 0.5f * (-vector + v - v2);
        var v6 = translation + 0.5f * (vector + v - v2);
        var v7 = translation + 0.5f * (-vector - v + v2);
        var v8 = translation + 0.5f * (vector - v + v2);
        var v9 = translation + 0.5f * (-vector + v + v2);
        var v10 = translation + 0.5f * (vector + v + v2);
        if (environmentData.ViewProjectionMatrix.HasValue)
        {
            var m = environmentData.ViewProjectionMatrix.Value;
            Vector3.Transform(ref v3, ref m, out v3);
            Vector3.Transform(ref v4, ref m, out v4);
            Vector3.Transform(ref v5, ref m, out v5);
            Vector3.Transform(ref v6, ref m, out v6);
            Vector3.Transform(ref v7, ref m, out v7);
            Vector3.Transform(ref v8, ref m, out v8);
            Vector3.Transform(ref v9, ref m, out v9);
            Vector3.Transform(ref v10, ref m, out v10);
        }

        var num = Terrain.ExtractContents(value);
        var block = Blocks[num];
        var vector2 = Vector4.Zero;
        var textureSlotCount = block.GetTextureSlotCount(value);
        var textureSlot = block.GetFaceTextureSlot(0, value);
        vector2.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector2.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector2.W = vector2.Y + 1f / textureSlotCount;
        vector2.Z = vector2.X + 1f / textureSlotCount;
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnly(color, LightingManager.CalculateLighting(-matrix.Forward)), p1: v3, p2: v5,
            p3: v6, p4: v4, texCoord1: new Vector2(vector2.X, vector2.W), texCoord2: new Vector2(vector2.X, vector2.Y),
            texCoord3: new Vector2(vector2.Z, vector2.Y), texCoord4: new Vector2(vector2.Z, vector2.W));
        textureSlot = block.GetFaceTextureSlot(2, value);
        vector2.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector2.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector2.W = vector2.Y + 1f / textureSlotCount;
        vector2.Z = vector2.X + 1f / textureSlotCount;
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnly(color, LightingManager.CalculateLighting(matrix.Forward)), p1: v7, p2: v8,
            p3: v10, p4: v9, texCoord1: new Vector2(vector2.Z, vector2.W), texCoord2: new Vector2(vector2.X, vector2.W),
            texCoord3: new Vector2(vector2.X, vector2.Y), texCoord4: new Vector2(vector2.Z, vector2.Y));
        textureSlot = block.GetFaceTextureSlot(5, value);
        vector2.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector2.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector2.W = vector2.Y + 1f / textureSlotCount;
        vector2.Z = vector2.X + 1f / textureSlotCount;
        texturedBatch3D.QueueQuad(color: Color.MultiplyColorOnly(color, LightingManager.CalculateLighting(-matrix.Up)),
            p1: v3, p2: v4, p3: v8, p4: v7, texCoord1: new Vector2(vector2.X, vector2.Y),
            texCoord2: new Vector2(vector2.Z, vector2.Y), texCoord3: new Vector2(vector2.Z, vector2.W),
            texCoord4: new Vector2(vector2.X, vector2.W));
        textureSlot = block.GetFaceTextureSlot(4, value);
        vector2.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector2.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector2.W = vector2.Y + 1f / textureSlotCount;
        vector2.Z = vector2.X + 1f / textureSlotCount;
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnly(topColor, LightingManager.CalculateLighting(matrix.Up)), p1: v5, p2: v9,
            p3: v10, p4: v6, texCoord1: new Vector2(vector2.X, vector2.W), texCoord2: new Vector2(vector2.X, vector2.Y),
            texCoord3: new Vector2(vector2.Z, vector2.Y), texCoord4: new Vector2(vector2.Z, vector2.W));
        textureSlot = block.GetFaceTextureSlot(1, value);
        vector2.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector2.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector2.W = vector2.Y + 1f / textureSlotCount;
        vector2.Z = vector2.X + 1f / textureSlotCount;
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnly(color, LightingManager.CalculateLighting(-matrix.Right)), p1: v3, p2: v7,
            p3: v9, p4: v5, texCoord1: new Vector2(vector2.Z, vector2.W), texCoord2: new Vector2(vector2.X, vector2.W),
            texCoord3: new Vector2(vector2.X, vector2.Y), texCoord4: new Vector2(vector2.Z, vector2.Y));
        textureSlot = block.GetFaceTextureSlot(3, value);
        vector2.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector2.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector2.W = vector2.Y + 1f / textureSlotCount;
        vector2.Z = vector2.X + 1f / textureSlotCount;
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnly(color, LightingManager.CalculateLighting(matrix.Right)), p1: v4, p2: v6,
            p3: v10, p4: v8, texCoord1: new Vector2(vector2.X, vector2.W), texCoord2: new Vector2(vector2.X, vector2.Y),
            texCoord3: new Vector2(vector2.Z, vector2.Y), texCoord4: new Vector2(vector2.Z, vector2.W));
    }

    public static void DrawFlatOrImageExtrusionBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        float size,
        ref Matrix matrix,
        Texture2D? texture,
        Color color,
        bool isEmissive,
        DrawBlockEnvironmentData? environmentData
    )
    {
        environmentData ??= _defaultEnvironmentData;
        if (_drawImageExtrusionEnabled &&
            texture == null &&
            !isEmissive &&
            environmentData.DrawBlockMode is DrawBlockMode.FirstPerson or DrawBlockMode.ThirdPerson)
        {
            DrawImageExtrusionBlock(primitivesRenderer, value, size, ref matrix, color, environmentData);
            return;
        }

        DrawFlatBlock(primitivesRenderer, value, size, ref matrix, texture, color, isEmissive, environmentData);
    }

    public static void DrawFlatBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        float size,
        ref Matrix matrix,
        Texture2D? texture,
        Color color,
        bool isEmissive,
        DrawBlockEnvironmentData? environmentData
    )
    {
        environmentData ??= _defaultEnvironmentData;
        var num = Terrain.ExtractContents(value);
        var block = Blocks[num];
        var vector = Vector4.Zero;
        var textureSlotCount = block.GetTextureSlotCount(value);
        var textureSlot = block.GetFaceTextureSlot(-1, value);
        vector.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector.W = vector.Y + 1f / textureSlotCount;
        vector.Z = vector.X + 1f / textureSlotCount;
        texture ??= environmentData.SubsystemTerrain != null
            ? environmentData.SubsystemTerrain.SubsystemAnimatedTextures.AnimatedBlocksTexture
            : BlocksTexturesManager.DefaultBlocksTexture;
        if (!isEmissive)
        {
            var s = LightingManager.LightIntensityByLightValue[environmentData.Light];
            color = Color.MultiplyColorOnly(color, s);
        }

        var translation = matrix.Translation;
        Vector3 vector2;
        Vector3 vector3;
        if (environmentData.BillboardDirection.HasValue)
        {
            vector2 = Vector3.Normalize(Vector3.Cross(environmentData.BillboardDirection.Value, Vector3.UnitY));
            vector3 = -Vector3.Normalize(Vector3.Cross(environmentData.BillboardDirection.Value, vector2));
        }
        else
        {
            vector2 = matrix.Right;
            vector3 = matrix.Up;
        }

        var v = translation + 0.85f * size * (-vector2 - vector3);
        var v2 = translation + 0.85f * size * (vector2 - vector3);
        var v3 = translation + 0.85f * size * (-vector2 + vector3);
        var v4 = translation + 0.85f * size * (vector2 + vector3);
        if (environmentData.ViewProjectionMatrix.HasValue)
        {
            var m = environmentData.ViewProjectionMatrix.Value;
            Vector3.Transform(ref v, ref m, out v);
            Vector3.Transform(ref v2, ref m, out v2);
            Vector3.Transform(ref v3, ref m, out v3);
            Vector3.Transform(ref v4, ref m, out v4);
        }

        var texturedBatch3D = primitivesRenderer.TexturedBatch(texture, true, 0, null,
            RasterizerState.CullCounterClockwiseScissor, BlendState.AlphaBlend, SamplerState.PointClamp);
        texturedBatch3D.QueueQuad(v, v3, v4, v2, new Vector2(vector.X, vector.W), new Vector2(vector.X, vector.Y),
            new Vector2(vector.Z, vector.Y), new Vector2(vector.Z, vector.W), color);
        if (!environmentData.BillboardDirection.HasValue)
        {
            texturedBatch3D.QueueQuad(v, v2, v4, v3, new Vector2(vector.X, vector.W), new Vector2(vector.Z, vector.W),
                new Vector2(vector.Z, vector.Y), new Vector2(vector.X, vector.Y), color);
        }
    }

    private static void DrawImageExtrusionBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        float size,
        ref Matrix matrix,
        Color color,
        DrawBlockEnvironmentData? environmentData
    )
    {
        environmentData ??= _defaultEnvironmentData;
        var num = Terrain.ExtractContents(value);
        var block = Blocks[num];
        try
        {
            Image image;
            if (environmentData.SubsystemTerrain != null)
            {
                image = environmentData.SubsystemTerrain.SubsystemAnimatedTextures.AnimatedBlocksTexture.Tag as Image ??
                        throw new InvalidOperationException("Required iamge is null");
            }
            else
            {
                image = BlocksTexturesManager.DefaultBlocksTexture.Tag as Image ??
                        throw new InvalidOperationException("Required iamge is null");
            }

            var imageExtrusionBlockMesh = GetImageExtrusionBlockMesh(image, block.GetFaceTextureSlot(-1, value));
            DrawMeshBlock(primitivesRenderer, imageExtrusionBlockMesh, color, 1.7f * size, ref matrix, environmentData);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private static BlockMesh GetImageExtrusionBlockMesh(Image image, int slot)
    {
        var imageExtrusionKey = default(ImageExtrusionKey);
        imageExtrusionKey.Image = image;
        imageExtrusionKey.Slot = slot;
        var key = imageExtrusionKey;
        if (_imageExtrusionsCache.TryGetValue(key, out var value))
        {
            return value;
        }

        value = new BlockMesh();
        var num = (int)MathUtils.Round(_slotTexCoords[slot].X * image.Width);
        var num2 = (int)MathUtils.Round(_slotTexCoords[slot].Y * image.Height);
        var num3 = (int)MathUtils.Round(_slotTexCoords[slot].Z * image.Width);
        var num4 = (int)MathUtils.Round(_slotTexCoords[slot].W * image.Height);
        var num5 = MathUtils.Max(num3 - num, num4 - num2);
        value.AppendImageExtrusion(
            image: image,
            bounds: new Rectangle(left: num, top: num2, width: num3 - num, height: num4 - num2),
            scale: new Vector3(1f / num5, 1f / num5, 0.0833333358f),
            color: Color.White,
            alphaThreshold: 0
        );
        _imageExtrusionsCache.Add(key, value);

        return value;
    }

    public static void DrawMeshBlock(
        PrimitivesRenderer3D primitivesRenderer,
        BlockMesh blockMesh,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData? environmentData
    )
    {
        environmentData ??= _defaultEnvironmentData;
        var texture = environmentData.SubsystemTerrain != null
            ? environmentData.SubsystemTerrain.SubsystemAnimatedTextures.AnimatedBlocksTexture
            : BlocksTexturesManager.DefaultBlocksTexture;
        DrawMeshBlock(primitivesRenderer, blockMesh, texture, Color.White, size, ref matrix, environmentData);
    }

    public static void DrawMeshBlock(
        PrimitivesRenderer3D primitivesRenderer,
        BlockMesh? blockMesh,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData? environmentData
    )
    {
        environmentData ??= _defaultEnvironmentData;
        var texture = environmentData.SubsystemTerrain != null
            ? environmentData.SubsystemTerrain.SubsystemAnimatedTextures.AnimatedBlocksTexture
            : BlocksTexturesManager.DefaultBlocksTexture;
        DrawMeshBlock(primitivesRenderer, blockMesh, texture, color, size, ref matrix, environmentData);
    }

    public static void DrawMeshBlock(
        PrimitivesRenderer3D primitivesRenderer,
        BlockMesh? blockMesh,
        Texture2D texture,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData? environmentData
    )
    {
        environmentData ??= _defaultEnvironmentData;
        var num = LightingManager.LightIntensityByLightValue[environmentData.Light];
        var v = new Vector4(color);
        v.X *= num;
        v.Y *= num;
        v.Z *= num;
        var flag = v == Vector4.One;
        var texturedBatch3D = primitivesRenderer.TexturedBatch(texture, true, 0, null,
            RasterizerState.CullCounterClockwiseScissor, null, SamplerState.PointClamp);
        var flag2 = false;
        var m = !environmentData.ViewProjectionMatrix.HasValue
            ? matrix
            : matrix * environmentData.ViewProjectionMatrix.Value;
        if (size.UncloseTo(1f))
        {
            m = Matrix.CreateScale(size) * m;
        }

        if (m.M14 != 0f || m.M24 != 0f || m.M34 != 0f || m.M44.UncloseTo(1f))
        {
            flag2 = true;
        }

        if (blockMesh is null)
        {
            return;
        }

        var count = blockMesh.Vertices.Count;
        var array = blockMesh.Vertices.Array;
        var count2 = blockMesh.Indices.Count;
        var array2 = blockMesh.Indices.Array;
        var triangleVertices = texturedBatch3D.TriangleVertices;
        var count3 = triangleVertices.Count;
        var count4 = triangleVertices.Count;
        triangleVertices.Count += count;
        for (var i = 0; i < count; i++)
        {
            var blockMeshVertex = array[i];
            if (flag2)
            {
                var v2 = new Vector4(blockMeshVertex.Position, 1f);
                Vector4.Transform(ref v2, ref m, out v2);
                var num2 = 1f / v2.W;
                blockMeshVertex.Position = new Vector3(v2.X * num2, v2.Y * num2, v2.Z * num2);
            }
            else
            {
                Vector3.Transform(ref blockMeshVertex.Position, ref m, out blockMeshVertex.Position);
            }

            if (flag || blockMeshVertex.IsEmissive)
            {
                triangleVertices.Array[count4++] = new VertexPositionColorTexture(blockMeshVertex.Position,
                    blockMeshVertex.Color, blockMeshVertex.TextureCoordinates);
                continue;
            }

            var color2 = new Color((byte)(blockMeshVertex.Color.R * v.X), (byte)(blockMeshVertex.Color.G * v.Y),
                (byte)(blockMeshVertex.Color.B * v.Z), (byte)(blockMeshVertex.Color.A * v.W));
            triangleVertices.Array[count4++] = new VertexPositionColorTexture(blockMeshVertex.Position, color2,
                blockMeshVertex.TextureCoordinates);
        }

        var triangleIndices = texturedBatch3D.TriangleIndices;
        var count5 = triangleIndices.Count;
        triangleIndices.Count += count2;
        for (var j = 0; j < count2; j++)
        {
            triangleIndices.Array[count5++] = (ushort)(count3 + array2[j]);
        }
    }

    public static int DamageItem(int value, int damageCount)
    {
        var num = Terrain.ExtractContents(value);
        var block = Blocks[num];
        if (block.Durability >= 0)
        {
            var num2 = block.GetDamage(value) + damageCount;
            if (num2 <= block.Durability)
            {
                return block.SetDamage(value, num2);
            }

            return block.GetDamageDestructionValue(value);
        }

        return value;
    }

    public static void LoadBlocksData(string data)
    {
        data = data.Replace("\r", string.Empty);
        var blockDataArray = data.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
        var firstDataContentArray = blockDataArray[0].Split(';');
        var restDataArray = new string[firstDataContentArray.Length - 1];
        Array.Copy(
            sourceArray: firstDataContentArray,
            sourceIndex: 1,
            destinationArray: restDataArray,
            destinationIndex: 0,
            length: firstDataContentArray.Length - 1
        );
        for (var i = 1; i < blockDataArray.Length; i++)
        {
            if (string.IsNullOrEmpty(blockDataArray[i]))
            {
                continue;
            }

            var dataContentArray = blockDataArray[i].Split(';');

            if (dataContentArray.Length != restDataArray.Length + 1)
            {
                throw new InvalidOperationException(string.Format(LanguageManager.Get("BlocksManager", 2),
                    dataContentArray.Length != 0 ? dataContentArray[0] : LanguageManager.Unknown));
            }

            var typeName = dataContentArray[0];
            if (string.IsNullOrEmpty(typeName))
            {
                continue;
            }

            var block = Blocks.FirstOrDefault(v => v.GetType().Name == typeName);
            if (block == null)
            {
                throw new InvalidOperationException(string.Format(LanguageManager.Get("BlocksManager", 3), typeName));
            }

            var fieldInfoDict = block.GetType().GetRuntimeFields()
                .Where(runtimeField => runtimeField is { IsPublic: true, IsStatic: false })
                .ToDictionary(runtimeField => runtimeField.Name);

            for (var j = 1; j < dataContentArray.Length; j++)
            {
                var text = restDataArray[j - 1];
                var text2 = dataContentArray[j];
                if (string.IsNullOrEmpty(text2))
                {
                    continue;
                }

                if (!fieldInfoDict.TryGetValue(text, out var value))
                {
                    throw new InvalidOperationException(
                        string.Format(LanguageManager.Get("BlocksManager", 5), text));
                }

                object? obj;
                if (text2.StartsWith('#'))
                {
                    var refTypeName = text2[1..];
                    obj = !string.IsNullOrEmpty(refTypeName)
                        ? (Blocks.FirstOrDefault(v => v.GetType().Name == refTypeName) ??
                           throw new InvalidOperationException(
                               string.Format(LanguageManager.Get("BlocksManager", 6),
                                   refTypeName))).BlockIndex
                        : (object)block.BlockIndex;
                }
                else
                {
                    obj = HumanReadableConverter.ConvertFromString(value.FieldType, text2);
                }

                value.SetValue(block, obj);
            }
        }
    }

    private static void CalculateSlotTexCoordTables()
    {
        for (var i = 0; i < 256; i++)
        {
            _slotTexCoords[i] = TextureSlotToTextureCoords(i);
        }
    }

    private static Vector4 TextureSlotToTextureCoords(int slot)
    {
        var num = slot % 16;
        var num2 = slot / 16;
        var x = (num + 0.001f) / 16f;
        var y = (num2 + 0.001f) / 16f;
        var z = (num + 1 - 0.001f) / 16f;
        var w = (num2 + 1 - 0.001f) / 16f;
        return new Vector4(x, y, z, w);
    }

    public static Vector4[] GetslotTexCoords(int textureSlotCount)
    {
        var totalCount = textureSlotCount * textureSlotCount;
        var slotTexCoords = new Vector4[totalCount];
        for (var i = 0; i < totalCount; i++)
        {
            var num = i % textureSlotCount;
            var num2 = i / textureSlotCount;
            var x = (num + 0.001f) / textureSlotCount;
            var y = (num2 + 0.001f) / textureSlotCount;
            var z = (num + 1 - 0.001f) / textureSlotCount;
            var w = (num2 + 1 - 0.001f) / textureSlotCount;
            slotTexCoords[i] = new Vector4(x, y, z, w);
        }

        return slotTexCoords;
    }

    public static Block? GetBlock(string modSpace, string typeFullName)
    {
        var runtime = CurrentModRuntime.Value;
        return runtime?.Blocks.ById.Values
            .Where(entry => string.Equals(entry.Id.Namespace.Value, modSpace, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Block)
            .FirstOrDefault(block => string.Equals(block.GetType().Name, typeFullName, StringComparison.Ordinal));
    }

    public struct ImageExtrusionKey : IEquatable<ImageExtrusionKey>
    {
        public Image Image;

        public int Slot;

        public override int GetHashCode()
        {
            return Image.GetHashCode() ^ Slot.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return obj is ImageExtrusionKey key && Equals(key);
        }

        public bool Equals(ImageExtrusionKey other)
        {
            return Image.Equals(other.Image) && Slot == other.Slot;
        }
    }
}
