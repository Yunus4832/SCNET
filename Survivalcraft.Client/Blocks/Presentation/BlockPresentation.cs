using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class BlockPresentation(Block block, BlockPresentationCatalog catalog) : IDisposable
{
    public Block Block { get; } = block;

    public BlockPresentationCatalog Catalog { get; } = catalog;

    protected BlockContentCatalog RulesCatalog => Block.Catalog;

    public virtual void Initialize()
    {
    }

    public virtual void Dispose()
    {
    }

    public string Category = string.Empty;

    public string Description = string.Empty;

    public string DisplayName = string.Empty;

    public Vector3 IconBlockOffset = Vector3.Zero;

    public Vector3 IconViewOffset = new(1f);

    public float IconViewScale = 1f;

    public int ShadowStrength;

    public int TextureSlot;

    public float DestructionDebrisScale = 1f;

    public int DisplayOrder;

    public Vector3 FirstPersonOffset = Vector3.Zero;

    public Vector3 FirstPersonRotation = Vector3.Zero;

    public float FirstPersonScale = 1f;

    public bool GenerateFacesForSameNeighbors;

    public Vector3 InHandOffset = Vector3.Zero;

    public Vector3 InHandRotation = Vector3.Zero;

    public float InHandScale = 1f;


    public float ObjectShadowStrength;

    public virtual Vector3 GetFirstPersonOffset(int value)
    {
        return FirstPersonOffset;
    }

    public virtual Vector3 GetFirstPersonRotation(int value)
    {
        return FirstPersonRotation;
    }

    public virtual float GetInHandScale(int value)
    {
        return InHandScale;
    }

    public virtual Vector3 GetInHandOffset(int value)
    {
        return InHandOffset;
    }

    public virtual Vector3 GetInHandRotation(int value)
    {
        return InHandRotation;
    }

    public virtual float GetFirstPersonScale(int value)
    {
        return FirstPersonScale;
    }

    public virtual TerrainVertex SetDiggingCrackingTextureTransform(TerrainVertex vertex)
    {
        var b = (byte)((vertex.Color.R + vertex.Color.G + vertex.Color.B) / 3);
        vertex.Tx = (short)(vertex.Tx * 16f);
        vertex.Ty = (short)(vertex.Ty * 16f);
        vertex.Color = new Color(b, b, b, (byte)128);
        return vertex;
    }

    public virtual Texture2D GetDiggingCrackingTexture(
        ComponentMiner miner,
        float digProgress,
        int value,
        Texture2D[] defaultCrackTextures
    )
    {
        var num2 = MathUtils.Clamp((int)(digProgress * 8f), 0, 7);
        return defaultCrackTextures[num2];
    }

    public virtual float GetObjectShadowStrength(int value)
    {
        return ObjectShadowStrength;
    }

    public virtual string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var bn = $"{Block.GetType().Name}:{data}";
        return Localization.ClientLanguage.Catalog.TryGetBlock(bn, "DisplayName", out var result)
            ? result!
            : DisplayName;
    }

    public virtual int GetTextureSlotCount(int value)
    {
        return 16;
    }

    public virtual string GetDescription(int value)
    {
        var data = Terrain.ExtractData(value);
        var bn = $"{Block.GetType().Name}:{data}";
        return Localization.ClientLanguage.Catalog.TryGetBlock(bn, "Description", out var r) ? r! : Description;
    }

    public virtual string GetCategory(int value) => Category;

    public virtual bool ShouldGenerateFace(SubsystemTerrain subsystemTerrain, int face, int value, int neighborValue)
    {
        var num = Terrain.ExtractContents(neighborValue);
        return Block.Catalog.Blocks[num]
            .IsFaceTransparent(subsystemTerrain, CellFace.OppositeFace(face), neighborValue);
    }

    public virtual bool ShouldGenerateFace(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int neighborValue,
        int x,
        int y,
        int z
    )
    {
        var num = Terrain.ExtractContents(neighborValue);
        return Block.Catalog.Blocks[num]
            .IsFaceTransparent(subsystemTerrain, CellFace.OppositeFace(face), neighborValue);
    }

    public virtual int GetShadowStrength(int value) => ShadowStrength;

    public virtual int GetFaceTextureSlot(int face, int value) => TextureSlot;

    public abstract void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    );

    public virtual void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometrySubset geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
    }

    public abstract void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    );

    public virtual int GetDisplayOrder(int value) => DisplayOrder;

    public virtual Vector3 GetIconBlockOffset(int value, DrawBlockEnvironmentData environmentData) => IconBlockOffset;

    public virtual Vector3 GetIconViewOffset(int value, DrawBlockEnvironmentData environmentData) => IconViewOffset;

    public virtual float GetIconViewScale(int value, DrawBlockEnvironmentData environmentData) => IconViewScale;

    public virtual BlockDebrisParticleSystem? CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        if (DestructionDebrisScale <= 0f) { return null; }

        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            strength,
            DestructionDebrisScale,
            Color.White,
            GetFaceTextureSlot(4, value)
        );
    }

    public virtual void OnDug(SubsystemTerrain terrain, SubsystemParticles particles, BlockDigOutcome outcome)
    {
        if (!outcome.DebrisAllowed || DestructionDebrisScale <= 0f) { return; }

        if (CreateDebrisParticleSystem(terrain, outcome.HitPosition, outcome.OldValue, 0.65f) is { } debris)
        {
            particles.AddParticleSystem(debris);
        }
    }

    public virtual bool IsSwapAnimationNeeded(int oldValue, int newValue) => true;
}
