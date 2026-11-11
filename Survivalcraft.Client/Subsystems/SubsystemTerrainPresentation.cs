using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Terrains.Rendering;

namespace Game.Subsystems;

/// <summary>WorldConnection-only ownership of terrain meshes, textures and rendering.</summary>
public sealed class SubsystemTerrainPresentation : Subsystem, IDrawable, IUpdateable
{
    public static bool TerrainRenderingEnabled = true;

    public SubsystemTerrain TerrainSubsystem { get; private set; } = null!;

    public SubsystemAnimatedTextures SubsystemAnimatedTextures { get; private set; } = null!;

    public TerrainChunkPresentationStore ChunkPresentations { get; private set; } = null!;

    public TerrainRenderer TerrainRenderer { get; private set; } = null!;

    public BlockGeometryGenerator BlockGeometryGenerator { get; private set; } = null!;

    public TerrainMeshUpdater MeshUpdater { get; private set; } = null!;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Terrain + 1;

    private static readonly int[] _drawOrders = [0, 100];

    public int[] DrawOrders => _drawOrders;

    private bool _disposed;

    private bool _subscribed;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            throw new InvalidOperationException("Terrain presentation requires a client presentation world.");
        }

        try
        {
            TerrainSubsystem = Project.FindSubsystem<SubsystemTerrain>(true)!;
            SubsystemAnimatedTextures = Project.FindSubsystem<SubsystemAnimatedTextures>(true)!;
            ChunkPresentations = new TerrainChunkPresentationStore();
            BlockGeometryGenerator = new BlockGeometryGenerator(
                TerrainSubsystem.Terrain,
                TerrainSubsystem,
                Project.FindSubsystem<SubsystemElectricity>(true)!,
                TerrainSubsystem.SubsystemFurnitureBlockBehavior,
                Project.FindSubsystem<SubsystemMetersBlockBehavior>(true)!,
                TerrainSubsystem.SubsystemPalette
            );
            TerrainRenderer = new TerrainRenderer(this);
            MeshUpdater = new TerrainMeshUpdater(this);
            TerrainSubsystem.Closing += OnTerrainClosing;
            TerrainSubsystem.DestructionDebrisRequested += OnDestructionDebrisRequested;
            TerrainSubsystem.Residency.ChunkReleasing += OnChunkReleasing;
            TerrainSubsystem.Residency.ChunkAllocationRenewed += OnAllocationRenewed;
            TerrainSubsystem.MeshInvalidated += OnDerivedDataInvalidated;
            TerrainSubsystem.Computations.DerivedInputsInvalidated += OnDerivedInputsInvalidated;
            TerrainSubsystem.TerrainAppearanceChanged += OnEnvironmentChanged;
            if (TerrainSubsystem.ClientChunkContents is { } contents)
            {
                contents.ContentInstalled += OnContentInstalled;
            }

            Configuration.ClientConfiguration.BrightnessChanged += OnBrightnessChanged;
            _subscribed = true;
        }
        catch (Exception constructionError)
        {
            try { Dispose(); }
            catch (Exception cleanupError) { throw new AggregateException(constructionError, cleanupError); }

            throw;
        }
    }

    public float GetUpdateProgress(int locationIndex, float visibilityDistance, float contentDistance) =>
        MeshUpdater.GetUpdateProgress(locationIndex, visibilityDistance, contentDistance);


    public void Update(float dt)
    {
        MeshUpdater.Update();
    }

    public void Draw(Camera camera, int drawOrder)
    {
        if (!TerrainRenderingEnabled) { return; }

        if (drawOrder == 0)
        {
            MeshUpdater.PrepareForDrawing(camera);
            TerrainRenderer.PrepareForDrawing(camera);
            TerrainRenderer.DrawOpaque(camera);
            TerrainRenderer.DrawAlphaTested(camera);
        }
        else if (drawOrder == 100) { TerrainRenderer.DrawTransparent(camera); }
    }

    private void OnContentInstalled(TerrainChunk chunk)
    {
        var presentation = ChunkPresentations.Get(chunk);
        presentation.NewGeometryData = false;
    }

    private void OnChunkReleasing(TerrainChunk chunk) => ChunkPresentations.Release(chunk);

    private void OnAllocationRenewed(TerrainChunk chunk, ulong previousGeneration) =>
        ChunkPresentations.RenewAllocation(chunk, previousGeneration);

    private void OnDerivedInputsInvalidated(TerrainChunk chunk)
    {
        // The computation scheduler owns requeueing after this notification.
        ChunkPresentations.Get(chunk).RequestMeshRebuild();
    }

    private void OnDerivedDataInvalidated(TerrainChunk chunk, bool discardCache)
    {
        var presentation = ChunkPresentations.Get(chunk);
        presentation.RequestMeshRebuild();
        if (discardCache) { presentation.InvalidateSliceContentsHashes(); }

        TerrainSubsystem.Computations.Request(chunk);
        // Deferred maintenance resumes in the presentation update once per frame.
    }

    private void OnDestructionDebrisRequested(int value, Vector3 position, float strength)
    {
        if (Project.FindSubsystem<SubsystemGameWidgets>(true)!.CalculateDistanceFromNearestView(position) >= 16f)
        {
            return;
        }

        var block = Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog
            .Get(Terrain.ExtractContents(value));
        var particles = block.CreateDebrisParticleSystem(TerrainSubsystem, position, value, strength);
        if (particles is not null)
        {
            Project.FindSubsystem<SubsystemParticles>(true)!.AddParticleSystem(particles);
        }
    }

    private void OnEnvironmentChanged() => MeshUpdater.RequestAllMeshesRebuild(false);
    private void OnBrightnessChanged() => MeshUpdater.RequestAllMeshesRebuild(true);
    private void OnTerrainClosing(SubsystemTerrain terrain) => Dispose();

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        var errors = new List<Exception>();
        if (_subscribed)
        {
            TerrainSubsystem.Closing -= OnTerrainClosing;
            TerrainSubsystem.DestructionDebrisRequested -= OnDestructionDebrisRequested;
            TerrainSubsystem.Residency.ChunkReleasing -= OnChunkReleasing;
            TerrainSubsystem.Residency.ChunkAllocationRenewed -= OnAllocationRenewed;
            TerrainSubsystem.MeshInvalidated -= OnDerivedDataInvalidated;
            TerrainSubsystem.Computations.DerivedInputsInvalidated -= OnDerivedInputsInvalidated;
            TerrainSubsystem.TerrainAppearanceChanged -= OnEnvironmentChanged;
            if (TerrainSubsystem.ClientChunkContents is { } contents)
            {
                contents.ContentInstalled -= OnContentInstalled;
            }

            Configuration.ClientConfiguration.BrightnessChanged -= OnBrightnessChanged;
            _subscribed = false;
        }

        TryCleanup(() => MeshUpdater?.Dispose());
        TryCleanup(() => TerrainRenderer?.Dispose());
        TryCleanup(() => ChunkPresentations?.Dispose());
        if (errors.Count > 0)
        {
            throw new AggregateException("Could not dispose all terrain presentation resources.", errors);
        }

        void TryCleanup(Action cleanup)
        {
            try { cleanup(); }
            catch (Exception exception) { errors.Add(exception); }
        }
    }
}
