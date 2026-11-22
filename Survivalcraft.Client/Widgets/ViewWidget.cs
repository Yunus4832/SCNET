using Engine.Graphics;

using Game.Network.Transport;

namespace Game.Widgets;

public class ViewWidget : TouchInputWidget, IDragTargetWidget
{
    private RenderTarget2D? _scalingRenderTarget;

    private SubsystemDrawing _subsystemDrawing = null!;

    private SubsystemTerrainPresentation? _terrainPresentation;

    private GameWidget GameWidget { get; set; } = null!;

    public Point2? ScalingRenderTargetSize
    {
        get
        {
            if (_scalingRenderTarget == null)
            {
                return null;
            }

            return new Point2(_scalingRenderTarget.Width, _scalingRenderTarget.Height);
        }
    }

    public void DragOver(Widget dragWidget, object data)
    {
    }

    public void DragDrop(Widget dragWidget, object data)
    {
        if (data is not InventoryDragData inventoryDragData)
        {
            return;
        }

        var screenPos = dragWidget.WidgetToScreen(dragWidget.ActualSize / 2f);
        var worldPos =
            Vector3.Normalize(
                GameWidget.ActiveCamera.ScreenToWorld(new Vector3(screenPos.X, screenPos.Y, 1f), Matrix.Identity) -
                GameWidget.ActiveCamera.ViewPosition
            ) *
            12f;
        var componentCharacter = GameWidget.CharacterData.ComponentCharacter;
        var count =
            componentCharacter != null &&
            componentCharacter.Entity.FindComponent<ComponentInput>(true)!.SplitSourceInventory ==
            inventoryDragData.Inventory &&
            componentCharacter.Entity.FindComponent<ComponentInput>(true)!.SplitSourceSlotIndex ==
            inventoryDragData.SlotIndex
                ? 1
                : inventoryDragData.DragMode != DragMode.SingleItem
                    ? inventoryDragData.Inventory.GetSlotCount(inventoryDragData.SlotIndex)
                    : MathUtils.Min(inventoryDragData.Inventory.GetSlotCount(inventoryDragData.SlotIndex), 1);
        NetworkSender.SendToServer(
            WorldRuntimeContext.For(GameWidget.CharacterData.Project),
            CharacterPackageBuilder.Capture(
                GameWidget.CharacterData,
                inventoryDragData.Inventory.Id,
                inventoryDragData.SlotIndex,
                worldPos,
                count
            )
        );
    }

    public void DragOut(Widget dragWidget, object data)
    {
    }

    public void DragIn(Widget dragWidget, object data)
    {
    }

    public override void ChangeParent(ContainerWidget? parentWidget)
    {
        if (parentWidget is not GameWidget widget)
        {
            throw new InvalidOperationException("ViewWidget must be a child of GameWidget.");
        }

        GameWidget = widget;
        var project = GameWidget.SubsystemGameWidgets.Project;
        _subsystemDrawing = project.FindSubsystem<SubsystemDrawing>(true)!;
        _terrainPresentation = project.FindSubsystem<SubsystemTerrainPresentation>();
        base.ChangeParent(widget);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
        base.MeasureOverride(parentAvailableSize);
    }

    public override void Draw(DrawContext dc)
    {
        if (GameWidget.CharacterData.ComponentCharacter is null || !GameWidget.Presentation.IsReadyForPlaying)
        {
            return;
        }

        if (GameWidget.CharacterData.IsLocalCharacter)
        {
            DrawToScreen(dc);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _scalingRenderTarget?.Dispose();
        _scalingRenderTarget = null!;
    }

    public void SetupScalingRenderTarget()
    {
        var resolutionScale = Configuration.ClientConfiguration.Preferences.ResolutionMode == ResolutionMode.Low
            ? 0.5f
            : Configuration.ClientConfiguration.Preferences.ResolutionMode != ResolutionMode.Medium
                ? 1f
                : 0.75f;
        var horizontalScale = GlobalTransform.Right.Length();
        var verticalScale = GlobalTransform.Up.Length();
        var vector = new Vector2(ActualSize.X * horizontalScale, ActualSize.Y * verticalScale);
        Point2 point = default;
        point.X = (int)MathUtils.Round(vector.X * resolutionScale);
        point.Y = (int)MathUtils.Round(vector.Y * resolutionScale);
        var point2 = point;
        if ((resolutionScale < 1f || GlobalColorTransform != Color.White) && point2.X > 0 && point2.Y > 0)
        {
            if (_scalingRenderTarget == null ||
                _scalingRenderTarget.Width != point2.X ||
                _scalingRenderTarget.Height != point2.Y)
            {
                _scalingRenderTarget?.Dispose();
                _scalingRenderTarget = null!;
                _scalingRenderTarget = new RenderTarget2D(
                    point2.X,
                    point2.Y,
                    1,
                    ColorFormat.Rgba8888,
                    DepthFormat.Depth24Stencil8
                );
            }

            Display.RenderTarget = _scalingRenderTarget;
            Display.Clear(Color.Black, 1f, 0);
        }
        else
        {
            _scalingRenderTarget?.Dispose();
            _scalingRenderTarget = null!;
        }
    }

    public void ApplyScalingRenderTarget(DrawContext dc)
    {
        if (_scalingRenderTarget != null)
        {
            var blendState = GlobalColorTransform.A < byte.MaxValue ? BlendState.AlphaBlend : BlendState.Opaque;
            var texturedBatch2D = dc.PrimitivesRenderer2D.TexturedBatch(
                _scalingRenderTarget,
                false,
                0,
                DepthStencilState.None,
                RasterizerState.CullNoneScissor,
                blendState,
                SamplerState.PointClamp
            );
            var count = texturedBatch2D.TriangleVertices.Count;
            texturedBatch2D.QueueQuad(Vector2.Zero, ActualSize, 0f, Vector2.Zero, Vector2.One, GlobalColorTransform);
            texturedBatch2D.TransformTriangles(GlobalTransform, count);
            dc.PrimitivesRenderer2D.Flush();
        }
    }

    public void DrawToScreen(DrawContext dc)
    {
        var camera = GameWidget.ActiveCamera;
        camera.PrepareForDrawing(null);
        var renderTarget = Display.RenderTarget;
        SetupScalingRenderTarget();
        try
        {
            _terrainPresentation?.PrepareView(camera);
            _subsystemDrawing.Draw(camera);
        }
        finally
        {
            Display.RenderTarget = renderTarget;
        }

        ApplyScalingRenderTarget(dc);
    }
}
