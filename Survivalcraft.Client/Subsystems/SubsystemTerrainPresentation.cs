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

    internal TerrainEditCohorts EditCohorts { get; private set; } = null!;

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
            EditCohorts = new TerrainEditCohorts(
                TerrainSubsystem.Terrain,
                TerrainSubsystem.Computations.SetInteractive
            );
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
            TerrainSubsystem.CellGeometryChanged += OnCellGeometryChanged;
            TerrainSubsystem.Computations.DerivedInputsInvalidated += OnDerivedInputsInvalidated;
            TerrainSubsystem.TerrainAppearanceChanged += OnEnvironmentChanged;
            if (TerrainSubsystem.CellPrediction is { } prediction)
            {
                prediction.GeometryChanged += TrackInteractiveCell;
            }

            if (TerrainSubsystem.ClientChunkContents is { } contents)
            {
                contents.ContentInstalled += OnContentInstalled;
            }

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
        if (!TerrainRenderingEnabled || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

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
        if (EditCohorts.Find(chunk) is { } group)
        {
            foreach (var member in group) { ChunkPresentations.Get(member).MarkInteractiveSnapshot(); }
        }
        else if (presentation.GeometryUploaded) { presentation.MarkInteractiveSnapshot(); }

        presentation.NewGeometryData = false;
    }

    private void TrackInteractiveCell(Point3 cell)
    {
        EditCohorts.Track(cell);
        var owner = TerrainSubsystem.Terrain.GetChunkAtCell(cell.X, cell.Z, false);
        if (owner == null) { return; }

        if (EditCohorts.Find(owner) is { } group)
        {
            foreach (var chunk in group) { ChunkPresentations.Get(chunk).MarkInteractiveCell(cell.Y); }
        }
        else { ChunkPresentations.Get(owner).MarkInteractiveCell(cell.Y); }
    }

    private void OnCellGeometryChanged(Point3 cell, int before, int after)
    {
        if ((cell.X & 15) is not (0 or 15) && (cell.Z & 15) is not (0 or 15)) { return; }

        var oldContents = Terrain.ExtractContents(before);
        var newContents = Terrain.ExtractContents(after);
        var oldBlock = TerrainSubsystem.Catalog.Blocks[oldContents];
        var newBlock = TerrainSubsystem.Catalog.Blocks[newContents];
        var faces = new List<int>();
        if ((cell.X & 15) == 0) { faces.Add(3); }

        if ((cell.X & 15) == 15) { faces.Add(1); }

        if ((cell.Z & 15) == 0) { faces.Add(2); }

        if ((cell.Z & 15) == 15) { faces.Add(0); }

        if (!faces.Any(face => oldBlock.IsFaceTransparent(TerrainSubsystem, face, before) !=
                               newBlock.IsFaceTransparent(TerrainSubsystem, face, after)
            )) { return; }

        var chunk = TerrainSubsystem.Terrain.GetChunkAtCell(cell.X, cell.Z, false);
        if (chunk != null && ChunkPresentations.TryGet(chunk, out var mesh) && mesh.GeometryUploaded)
        {
            TrackInteractiveCell(cell);
        }
    }

    private void OnChunkReleasing(TerrainChunk chunk)
    {
        TerrainRenderer.InvalidateStagedGeometry(chunk);
        EditCohorts.Release(chunk);
        ChunkPresentations.Release(chunk);
    }

    private void OnAllocationRenewed(TerrainChunk chunk, ulong previousGeneration)
    {
        TerrainRenderer.InvalidateStagedGeometry(chunk);
        EditCohorts.Release(chunk);
        ChunkPresentations.RenewAllocation(chunk, previousGeneration);
    }

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
            TerrainSubsystem.CellGeometryChanged -= OnCellGeometryChanged;
            TerrainSubsystem.Computations.DerivedInputsInvalidated -= OnDerivedInputsInvalidated;
            TerrainSubsystem.TerrainAppearanceChanged -= OnEnvironmentChanged;
            if (TerrainSubsystem.CellPrediction is { } prediction)
            {
                prediction.GeometryChanged -= TrackInteractiveCell;
            }

            if (TerrainSubsystem.ClientChunkContents is { } contents)
            {
                contents.ContentInstalled -= OnContentInstalled;
            }

            _subscribed = false;
        }

        TryCleanup(() => MeshUpdater?.Dispose());
        TryCleanup(() => TerrainRenderer?.Dispose());
        TryCleanup(() => EditCohorts?.Clear());
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
