using Engine.Core.Serialization;
using Engine.Graphics;
using Engine.Media.Images;

using Game.Blocks.Presentation;

using static Game.Managers.BlocksManager;

namespace Game.Modding.Blocks;

public sealed record BlockPresentationRegistration(
    ResourceId Id,
    Func<Block, BlockPresentationCatalog, BlockPresentation> Create
);

public sealed class BlockPresentationCatalog : IDisposable
{
    private readonly Dictionary<ResourceId, BlockPresentation> _byId = new();

    private readonly Dictionary<int, BlockPresentation> _byIndex = new();

    private readonly List<string> _categories =
    [
        "Terrain", "Minerals", "Plants", "Construction", "Items", "Tools", "Weapons", "Clothes", "Electrics", "Food",
        "Spawner Eggs", "Painted", "Dyed", "Fireworks"
    ];

    public ReadOnlyList<string> ReadOnlyCategories => new(_categories);

    private bool _disposed;

    private bool _initialized;

    public BlockContentCatalog Rules { get; }

    public FurnitureDesignPresentation Furniture { get; }

    public BlockPresentationCatalog(BlockContentCatalog rules, IEnumerable<BlockPresentationRegistration> registrations)
    {
        Rules = rules;
        Furniture = new FurnitureDesignPresentation(this);
        CalculateSlotTexCoordTables();
        try
        {
            foreach (var registration in registrations)
            {
                if (_byId.ContainsKey(registration.Id))
                {
                    throw new InvalidOperationException(
                        $"Duplicate block presentation registration for {registration.Id}."
                    );
                }

                var block = rules.GetBlock(registration.Id);
                var presentation = registration.Create(block, this);
                if (!ReferenceEquals(presentation.Block, block))
                {
                    throw new InvalidOperationException(
                        $"Presentation factory bound the wrong block for {registration.Id}."
                    );
                }

                _byId.Add(registration.Id, presentation);
                _byIndex.Add(block.BlockIndex, presentation);
                var fields = presentation.GetType().GetRuntimeFields()
                    .Where(field => field.IsPublic && !field.IsStatic).ToDictionary(field => field.Name);
                foreach (var value in rules.GetAssetValues(registration.Id))
                {
                    if (!fields.TryGetValue(value.Column, out var field))
                    {
                        throw new InvalidDataException(
                            $"Unknown presentation field {value.Column} for {registration.Id}."
                        );
                    }

                    field.SetValue(
                        presentation,
                        HumanReadableConverter.ConvertFromString(field.FieldType, value.Value)
                    );
                }
            }

            foreach (var block in rules.RegisteredBlocks)
            {
                if (!_byIndex.ContainsKey(block.BlockIndex))
                {
                    throw new InvalidOperationException(
                        $"Missing block presentation registration for index {block.BlockIndex}."
                    );
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public BlockPresentation Get(ResourceId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _byId[id];
    }

    public BlockPresentation Get(int runtimeIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _byIndex[runtimeIndex];
    }

    public IEnumerable<int> GetCreativeValues() => Rules.GetCreativeValues()
        .OrderBy(value => Get(Terrain.ExtractContents(value)).GetDisplayOrder(value));

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized) { throw new InvalidOperationException("Block presentations are already initialized."); }

        try
        {
            foreach (var presentation in _byId.Values)
            {
                presentation.Initialize();
                foreach (var value in presentation.Block.GetCreativeValues())
                {
                    AddCategory(presentation.GetCategory(value));
                }
            }

            _initialized = true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void AddCategory(string category)
    {
        if (!_categories.Contains(category))
        {
            _categories.Add(category);
        }
    }

    public void Dispose()
    {
        Furniture.Dispose();
        if (_disposed) { return; }

        _disposed = true;
        foreach (var presentation in _byId.Values)
        {
            try { presentation.Dispose(); }
            catch (Exception exception)
            {
                Log.Error($"Could not dispose block presentation {presentation.Block.BlockIndex}: {exception}");
            }
        }

        _byId.Clear();
        _byIndex.Clear();
        _imageExtrusionsCache.Clear();
    }

    private readonly DrawBlockEnvironmentData _defaultEnvironmentData = new();


    private readonly Vector4[] _slotTexCoords = new Vector4[256];


    private readonly Dictionary<ImageExtrusionKey, BlockMesh> _imageExtrusionsCache = new();


    private const bool _drawImageExtrusionEnabled = true;


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
            ? environmentData.SubsystemTerrain.Project.FindSubsystem<SubsystemAnimatedTextures>(true)!
                .AnimatedBlocksTexture
            : BlocksTexturesManager.DefaultBlocksTexture;
        var texturedBatch3D = primitivesRenderer.TexturedBatch(
            texture,
            true,
            0,
            null,
            RasterizerState.CullCounterClockwiseScissor,
            BlendState.AlphaBlend,
            SamplerState.PointClamp
        );
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
        var block = Get(num);
        var vector4 = _slotTexCoords[block.GetFaceTextureSlot(0, value)];
        vector4.W = MathUtils.Lerp(vector4.Y, vector4.W, height);
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnlyNotSaturated(color, LightingManager.CalculateLighting(-matrix.Forward)),
            p1: v,
            p2: v3,
            p3: v4,
            p4: v2,
            texCoord1: new Vector2(vector4.X, vector4.W),
            texCoord2: new Vector2(vector4.X, vector4.Y),
            texCoord3: new Vector2(vector4.Z, vector4.Y),
            texCoord4: new Vector2(vector4.Z, vector4.W)
        );
        vector4 = _slotTexCoords[block.GetFaceTextureSlot(2, value)];
        vector4.W = MathUtils.Lerp(vector4.Y, vector4.W, height);
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnlyNotSaturated(color, LightingManager.CalculateLighting(matrix.Forward)),
            p1: v5,
            p2: v6,
            p3: v8,
            p4: v7,
            texCoord1: new Vector2(vector4.Z, vector4.W),
            texCoord2: new Vector2(vector4.X, vector4.W),
            texCoord3: new Vector2(vector4.X, vector4.Y),
            texCoord4: new Vector2(vector4.Z, vector4.Y)
        );
        vector4 = _slotTexCoords[block.GetFaceTextureSlot(5, value)];
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnlyNotSaturated(color, LightingManager.CalculateLighting(-matrix.Up)),
            p1: v,
            p2: v2,
            p3: v6,
            p4: v5,
            texCoord1: new Vector2(vector4.X, vector4.Y),
            texCoord2: new Vector2(vector4.Z, vector4.Y),
            texCoord3: new Vector2(vector4.Z, vector4.W),
            texCoord4: new Vector2(vector4.X, vector4.W)
        );
        vector4 = _slotTexCoords[block.GetFaceTextureSlot(4, value)];
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnlyNotSaturated(topColor, LightingManager.CalculateLighting(matrix.Up)),
            p1: v3,
            p2: v7,
            p3: v8,
            p4: v4,
            texCoord1: new Vector2(vector4.X, vector4.W),
            texCoord2: new Vector2(vector4.X, vector4.Y),
            texCoord3: new Vector2(vector4.Z, vector4.Y),
            texCoord4: new Vector2(vector4.Z, vector4.W)
        );
        vector4 = _slotTexCoords[block.GetFaceTextureSlot(1, value)];
        vector4.W = MathUtils.Lerp(vector4.Y, vector4.W, height);
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnlyNotSaturated(color, LightingManager.CalculateLighting(-matrix.Right)),
            p1: v,
            p2: v5,
            p3: v7,
            p4: v3,
            texCoord1: new Vector2(vector4.Z, vector4.W),
            texCoord2: new Vector2(vector4.X, vector4.W),
            texCoord3: new Vector2(vector4.X, vector4.Y),
            texCoord4: new Vector2(vector4.Z, vector4.Y)
        );
        vector4 = _slotTexCoords[block.GetFaceTextureSlot(3, value)];
        vector4.W = MathUtils.Lerp(vector4.Y, vector4.W, height);
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnlyNotSaturated(color, LightingManager.CalculateLighting(matrix.Right)),
            p1: v2,
            p2: v4,
            p3: v8,
            p4: v6,
            texCoord1: new Vector2(vector4.X, vector4.W),
            texCoord2: new Vector2(vector4.X, vector4.Y),
            texCoord3: new Vector2(vector4.Z, vector4.Y),
            texCoord4: new Vector2(vector4.Z, vector4.W)
        );
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
                ? environmentData.SubsystemTerrain.Project.FindSubsystem<SubsystemAnimatedTextures>(true)!
                    .AnimatedBlocksTexture
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
        var texturedBatch3D = primitivesRenderer.TexturedBatch(
            texture,
            true,
            0,
            null,
            RasterizerState.CullCounterClockwiseScissor,
            null,
            SamplerState.PointClamp
        );
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
        var block = Get(num);
        var vector2 = Vector4.Zero;
        var textureSlotCount = block.GetTextureSlotCount(value);
        var textureSlot = block.GetFaceTextureSlot(0, value);
        vector2.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector2.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector2.W = vector2.Y + 1f / textureSlotCount;
        vector2.Z = vector2.X + 1f / textureSlotCount;
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnly(color, LightingManager.CalculateLighting(-matrix.Forward)),
            p1: v3,
            p2: v5,
            p3: v6,
            p4: v4,
            texCoord1: new Vector2(vector2.X, vector2.W),
            texCoord2: new Vector2(vector2.X, vector2.Y),
            texCoord3: new Vector2(vector2.Z, vector2.Y),
            texCoord4: new Vector2(vector2.Z, vector2.W)
        );
        textureSlot = block.GetFaceTextureSlot(2, value);
        vector2.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector2.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector2.W = vector2.Y + 1f / textureSlotCount;
        vector2.Z = vector2.X + 1f / textureSlotCount;
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnly(color, LightingManager.CalculateLighting(matrix.Forward)),
            p1: v7,
            p2: v8,
            p3: v10,
            p4: v9,
            texCoord1: new Vector2(vector2.Z, vector2.W),
            texCoord2: new Vector2(vector2.X, vector2.W),
            texCoord3: new Vector2(vector2.X, vector2.Y),
            texCoord4: new Vector2(vector2.Z, vector2.Y)
        );
        textureSlot = block.GetFaceTextureSlot(5, value);
        vector2.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector2.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector2.W = vector2.Y + 1f / textureSlotCount;
        vector2.Z = vector2.X + 1f / textureSlotCount;
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnly(color, LightingManager.CalculateLighting(-matrix.Up)),
            p1: v3,
            p2: v4,
            p3: v8,
            p4: v7,
            texCoord1: new Vector2(vector2.X, vector2.Y),
            texCoord2: new Vector2(vector2.Z, vector2.Y),
            texCoord3: new Vector2(vector2.Z, vector2.W),
            texCoord4: new Vector2(vector2.X, vector2.W)
        );
        textureSlot = block.GetFaceTextureSlot(4, value);
        vector2.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector2.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector2.W = vector2.Y + 1f / textureSlotCount;
        vector2.Z = vector2.X + 1f / textureSlotCount;
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnly(topColor, LightingManager.CalculateLighting(matrix.Up)),
            p1: v5,
            p2: v9,
            p3: v10,
            p4: v6,
            texCoord1: new Vector2(vector2.X, vector2.W),
            texCoord2: new Vector2(vector2.X, vector2.Y),
            texCoord3: new Vector2(vector2.Z, vector2.Y),
            texCoord4: new Vector2(vector2.Z, vector2.W)
        );
        textureSlot = block.GetFaceTextureSlot(1, value);
        vector2.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector2.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector2.W = vector2.Y + 1f / textureSlotCount;
        vector2.Z = vector2.X + 1f / textureSlotCount;
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnly(color, LightingManager.CalculateLighting(-matrix.Right)),
            p1: v3,
            p2: v7,
            p3: v9,
            p4: v5,
            texCoord1: new Vector2(vector2.Z, vector2.W),
            texCoord2: new Vector2(vector2.X, vector2.W),
            texCoord3: new Vector2(vector2.X, vector2.Y),
            texCoord4: new Vector2(vector2.Z, vector2.Y)
        );
        textureSlot = block.GetFaceTextureSlot(3, value);
        vector2.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector2.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector2.W = vector2.Y + 1f / textureSlotCount;
        vector2.Z = vector2.X + 1f / textureSlotCount;
        texturedBatch3D.QueueQuad(
            color: Color.MultiplyColorOnly(color, LightingManager.CalculateLighting(matrix.Right)),
            p1: v4,
            p2: v6,
            p3: v10,
            p4: v8,
            texCoord1: new Vector2(vector2.X, vector2.W),
            texCoord2: new Vector2(vector2.X, vector2.Y),
            texCoord3: new Vector2(vector2.Z, vector2.Y),
            texCoord4: new Vector2(vector2.Z, vector2.W)
        );
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
        var block = Get(num);
        var vector = Vector4.Zero;
        var textureSlotCount = block.GetTextureSlotCount(value);
        var textureSlot = block.GetFaceTextureSlot(-1, value);
        vector.X = (float)(textureSlot % textureSlotCount) / textureSlotCount;
        vector.Y = (float)(textureSlot / textureSlotCount) / textureSlotCount;
        vector.W = vector.Y + 1f / textureSlotCount;
        vector.Z = vector.X + 1f / textureSlotCount;
        texture ??= environmentData.SubsystemTerrain != null
            ? environmentData.SubsystemTerrain.Project.FindSubsystem<SubsystemAnimatedTextures>(true)!
                .AnimatedBlocksTexture
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

        var texturedBatch3D = primitivesRenderer.TexturedBatch(
            texture,
            true,
            0,
            null,
            RasterizerState.CullCounterClockwiseScissor,
            BlendState.AlphaBlend,
            SamplerState.PointClamp
        );
        texturedBatch3D.QueueQuad(
            v,
            v3,
            v4,
            v2,
            new Vector2(vector.X, vector.W),
            new Vector2(vector.X, vector.Y),
            new Vector2(vector.Z, vector.Y),
            new Vector2(vector.Z, vector.W),
            color
        );
        if (!environmentData.BillboardDirection.HasValue)
        {
            texturedBatch3D.QueueQuad(
                v,
                v2,
                v4,
                v3,
                new Vector2(vector.X, vector.W),
                new Vector2(vector.Z, vector.W),
                new Vector2(vector.Z, vector.Y),
                new Vector2(vector.X, vector.Y),
                color
            );
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
        var block = Get(num);
        try
        {
            Image image;
            if (environmentData.SubsystemTerrain != null)
            {
                image = environmentData.SubsystemTerrain.Project.FindSubsystem<SubsystemAnimatedTextures>(true)!
                            .AnimatedBlocksTexture.Tag as Image ??
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

    private void CalculateSlotTexCoordTables()
    {
        for (var i = 0; i < 256; i++)
        {
            _slotTexCoords[i] = TextureSlotToTextureCoords(i);
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
