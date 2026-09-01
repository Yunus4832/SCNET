using Engine.Graphics;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Widgets;

public class ViewWidget : TouchInputWidget, IDragTargetWidget
{
    private RenderTarget2D? _scalingRenderTarget;

    private SubsystemDrawing _subsystemDrawing = null!;

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
                GameWidget.ActiveCamera.ViewPosition) * 12f;
        var componentPlayer = GameWidget.PlayerData.ComponentPlayer;
        var count =
            componentPlayer != null &&
            componentPlayer.ComponentInput.SplitSourceInventory == inventoryDragData.Inventory &&
            componentPlayer.ComponentInput.SplitSourceSlotIndex == inventoryDragData.SlotIndex
                ? 1
                : inventoryDragData.DragMode != DragMode.SingleItem
                    ? inventoryDragData.Inventory.GetSlotCount(inventoryDragData.SlotIndex)
                    : MathUtils.Min(inventoryDragData.Inventory.GetSlotCount(inventoryDragData.SlotIndex), 1);
        if (GameManager.Project == null)
        {
            return;
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            CommonLib.Net.QueuePackage(new ComponentPlayerPackage(GameWidget.PlayerData,
                inventoryDragData.Inventory.Id, inventoryDragData.SlotIndex, worldPos, count));
        }
        else
        {
            NetDragDrop(worldPos, inventoryDragData, count);
        }
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
        _subsystemDrawing = GameWidget.SubsystemGameWidgets.Project.FindSubsystem<SubsystemDrawing>(true)!;
        base.ChangeParent(widget);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
        base.MeasureOverride(parentAvailableSize);
    }

    public override void Draw(DrawContext dc)
    {
        if (GameWidget.PlayerData is not { ComponentPlayer: not null, IsReadyForPlaying: true })
        {
            return;
        }

        if (CommonLib.WorkType == WorkType.Local || GameWidget.PlayerData.IsMainPlayer)
        {
            DrawToScreen(dc);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        Utilities.Dispose(ref _scalingRenderTarget!);
    }

    public void NetDragDrop(Vector3 worldPos, InventoryDragData inventoryDragData, int count)
    {
        if (GameManager.Project is null)
        {
            throw new InvalidOperationException("GameManager.Project is not initialized");
        }

        var subsystemPickables = GameManager.Project.FindSubsystem<SubsystemPickables>(true)!;
        var slotValue = inventoryDragData.Inventory.GetSlotValue(inventoryDragData.SlotIndex);
        var num = inventoryDragData.Inventory.RemoveSlotItems(inventoryDragData.SlotIndex, count);
        if (num > 0)
        {
            subsystemPickables.AddPickable(slotValue, num, GameWidget.ActiveCamera.ViewPosition, worldPos, null);
        }
    }

    public void SetupScalingRenderTarget()
    {
        var num = SettingsManager.Current.ResolutionMode == ResolutionMode.Low ? 0.5f :
            SettingsManager.Current.ResolutionMode != ResolutionMode.Medium ? 1f : 0.75f;
        var num2 = GlobalTransform.Right.Length();
        var num3 = GlobalTransform.Up.Length();
        var vector = new Vector2(ActualSize.X * num2, ActualSize.Y * num3);
        Point2 point = default;
        point.X = (int)MathUtils.Round(vector.X * num);
        point.Y = (int)MathUtils.Round(vector.Y * num);
        var point2 = point;
        if ((num < 1f || GlobalColorTransform != Color.White) && point2.X > 0 && point2.Y > 0)
        {
            if (_scalingRenderTarget == null || _scalingRenderTarget.Width != point2.X ||
                _scalingRenderTarget.Height != point2.Y)
            {
                Utilities.Dispose(ref _scalingRenderTarget);
                _scalingRenderTarget = new RenderTarget2D(point2.X, point2.Y, 1, ColorFormat.Rgba8888,
                    DepthFormat.Depth24Stencil8);
            }

            Display.RenderTarget = _scalingRenderTarget;
            Display.Clear(Color.Black, 1f, 0);
        }
        else
        {
            Utilities.Dispose(ref _scalingRenderTarget);
        }
    }

    public void ApplyScalingRenderTarget(DrawContext dc)
    {
        if (_scalingRenderTarget != null)
        {
            var blendState = GlobalColorTransform.A < byte.MaxValue ? BlendState.AlphaBlend : BlendState.Opaque;
            var texturedBatch2D = dc.PrimitivesRenderer2D.TexturedBatch(_scalingRenderTarget, false, 0,
                DepthStencilState.None, RasterizerState.CullNoneScissor, blendState, SamplerState.PointClamp);
            var count = texturedBatch2D.TriangleVertices.Count;
            texturedBatch2D.QueueQuad(Vector2.Zero, ActualSize, 0f, Vector2.Zero, Vector2.One, GlobalColorTransform);
            texturedBatch2D.TransformTriangles(GlobalTransform, count);
            dc.PrimitivesRenderer2D.Flush();
        }
    }

    public void DrawToScreen(DrawContext dc)
    {
        GameWidget.ActiveCamera.PrepareForDrawing(null);
        var renderTarget = Display.RenderTarget;
        SetupScalingRenderTarget();
        try
        {
            _subsystemDrawing.Draw(GameWidget.ActiveCamera);
        }
        finally
        {
            Display.RenderTarget = renderTarget;
        }

        ApplyScalingRenderTarget(dc);
    }
}
