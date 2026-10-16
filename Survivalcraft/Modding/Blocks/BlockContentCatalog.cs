using System.Xml.Linq;

using Engine.Graphics;
using Engine.Media;
using Engine.Serialization;

using static Game.Managers.BlocksManager;

namespace Game.Modding.Blocks;

public sealed class BlockContentCatalog : IDisposable
{
    private readonly List<string> _categories = [];

    private readonly DrawBlockEnvironmentData _defaultEnvironmentData = new();

    private readonly Vector4[] _slotTexCoords = new Vector4[256];

    private readonly Dictionary<ImageExtrusionKey, BlockMesh> _imageExtrusionsCache = new();

    private const bool _drawImageExtrusionEnabled = true;

    private readonly Dictionary<int, ResourceId> _blockIdsByIndex = [];

    private readonly List<Block> _registeredBlocks = [];

    public Block[] Blocks { get; private set; } = new Block[1024];

    public FluidBlock?[] FluidBlocks { get; private set; } = new FluidBlock[1024];

    public ReadOnlyList<string> ReadOnlyCategories => new(_categories);

    public IReadOnlyList<Block> RegisteredBlocks => _registeredBlocks;

    private bool _disposed;

    private bool _initialized;

    public BlockRuntimeCatalog Registry { get; }

    public CraftingRecipeCatalog Recipes { get; }

    internal BlockContentCatalog(BlockRuntimeCatalog registry)
    {
        Registry = registry;
        Recipes = new CraftingRecipeCatalog(this);
        CalculateSlotTexCoordTables();
        foreach (var entry in registry.ById.Values
                     .OrderBy(entry => entry.Id.Namespace != BuiltInContentMod.Manifest.ModId))
        {
            entry.Block.Catalog = this;
            Blocks[entry.RuntimeIndex] = entry.Block;
            _registeredBlocks.Add(entry.Block);
            _blockIdsByIndex.Add(entry.RuntimeIndex, entry.Id);
            if (entry.Block is FluidBlock fluid)
            {
                FluidBlocks[entry.RuntimeIndex] = fluid;
            }
        }

        var air = Blocks[0] ?? throw new InvalidOperationException("The catalog has no air block.");
        for (var index = 0; index < Blocks.Length; index++)
        {
            Blocks[index] ??= air;
        }
    }

    internal void Initialize(XElement? clothingData, XElement recipes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            throw new InvalidOperationException("This content catalog is already initialized.");
        }

        if (clothingData is not null)
        {
            foreach (var clothing in _registeredBlocks.OfType<ClothingBlock>())
            {
                clothing.SetInitializationData(clothingData);
            }
        }

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
        foreach (var dataEntry in Registry.DataEntries)
        {
            LoadBlocksData(dataEntry.Read());
        }

        foreach (var block in _registeredBlocks)
        {
            block.Initialize();
            if (string.IsNullOrEmpty(block.CraftingId))
            {
                block.CraftingId = block.GetType().Name;
            }

            foreach (var value in block.GetCreativeValues())
            {
                var category = block.GetCategory(value);
                AddCategory(category);
            }
        }

        Recipes.Initialize(recipes);
        _initialized = true;
    }

    public IEnumerable<int> GetCreativeValues()
    {
        return _registeredBlocks
            .SelectMany(block => block.GetCreativeValues())
            .OrderBy(value => Blocks[Terrain.ExtractContents(value)].GetDisplayOrder(value));
    }

    public bool TryGetBlockId(int runtimeIndex, out ResourceId id)
    {
        return _blockIdsByIndex.TryGetValue(runtimeIndex, out id);
    }

    public void AddCategory(string category)
    {
        if (!_categories.Contains(category))
        {
            _categories.Add(category);
        }
    }

    public Block[] FindBlocksByCraftingId(string craftingId)
    {
        return _registeredBlocks.Where(c => c.MatchCraftingId(craftingId)).ToArray();
    }

    // 新增高度渲染cube
    public void DrawCubeBlock(
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


    public void DrawCubeBlock(
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

    private void DrawCubeBlock(
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

    public void DrawFlatOrImageExtrusionBlock(
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

    public void DrawFlatBlock(
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

    private void DrawImageExtrusionBlock(
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

    private BlockMesh GetImageExtrusionBlockMesh(Image image, int slot)
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

    public int DamageItem(int value, int damageCount)
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

    internal void LoadBlocksData(string data)
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

            var block = GetBlock(ResourceId.Parse(typeName));
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
                        ? GetBlock(ResourceId.Parse(refTypeName)).BlockIndex
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

    private void CalculateSlotTexCoordTables()
    {
        for (var i = 0; i < 256; i++)
        {
            _slotTexCoords[i] = TextureSlotToTextureCoords(i);
        }
    }

    public Block GetBlock(ResourceId id) => Registry.TryGet(id, out var entry)
        ? entry!.Block
        : throw new KeyNotFoundException($"Block {id} is not registered in this catalog.");

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _imageExtrusionsCache.Clear();
        foreach (var block in _registeredBlocks)
        {
            try
            {
                block.Dispose();
            }
            catch (Exception exception)
            {
                Log.Error($"Could not dispose block {block.GetType().FullName}: {exception}");
            }
        }
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
