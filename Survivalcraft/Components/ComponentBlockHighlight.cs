using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentBlockHighlight : Component, IDrawable, IUpdateable
{
    private static readonly int[] _drawOrders = [1, 2000];

    private ComponentPlayer _componentPlayer = null!;

    private object? _highlightRaycastResult;

    private readonly PrimitivesRenderer3D _primitivesRenderer3D = new();

    private Shader _shader = null!;

    private SubsystemAnimatedTextures _subsystemAnimatedTextures = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    public Point3? NearbyEditableCell { get; set; }

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
#if SERVER
        return;
#else
        if (camera.GameWidget.PlayerData != _componentPlayer.PlayerData)
        {
            return;
        }

        if (drawOrder == _drawOrders[0])
        {
            DrawFillHighlight(camera);
            DrawOutlineHighlight(camera);
            DrawReticleHighlight(camera);
        }
        else
        {
            DrawRayHighlight(camera);
        }
#endif
    }

    public UpdateOrder UpdateOrder => UpdateOrder.BlockHighlight;

    public void Update(float dt)
    {
#if SERVER
        return;
#else
        var activeCamera = _componentPlayer.GameWidget.ActiveCamera;
        var ray = new Ray3?(new Ray3(activeCamera.ViewPosition, activeCamera.ViewDirection));
        NearbyEditableCell = null;
        if (ray.HasValue)
        {
            _highlightRaycastResult = _componentPlayer.ComponentMiner.Raycast(ray.Value, RaycastMode.Digging);
            if (_highlightRaycastResult is not TerrainRaycastResult terrainRaycastResult)
            {
                return;
            }

            if (!(terrainRaycastResult.Distance < 3f))
            {
                return;
            }

            var point = terrainRaycastResult.CellFace.Point;
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
            var obj = BlocksManager.Blocks[Terrain.ExtractContents(cellValue)];
            if (obj is CrossBlock)
            {
                terrainRaycastResult.Distance = MathUtils.Max(terrainRaycastResult.Distance, 0.1f);
                _highlightRaycastResult = terrainRaycastResult;
            }

            if (obj.IsEditable(cellValue))
            {
                NearbyEditableCell = terrainRaycastResult.CellFace.Point;
            }
        }
        else
        {
            _highlightRaycastResult = null;
        }
#endif
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
#if SERVER
        return;
#else
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemAnimatedTextures = Project.FindSubsystem<SubsystemAnimatedTextures>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _componentPlayer = Entity.FindComponent<ComponentPlayer>(true)!;
        _shader = new Shader(
            ModsManager.GetInPakOrStorageFile<string>("Shaders/Highlight", ".vsh"),
            ModsManager.GetInPakOrStorageFile<string>("Shaders/Highlight", ".psh"),
            new ShaderMacro("ShadowShader")
        );
#endif
    }

    public void DrawRayHighlight(Camera camera)
    {
        Ray3 ray = default;
        float num;
        if (_highlightRaycastResult is TerrainRaycastResult)
        {
            var obj = (TerrainRaycastResult)_highlightRaycastResult;
            ray = obj.Ray;
            num = MathUtils.Min(obj.Distance, 2f);
        }
        else if (_highlightRaycastResult is BodyRaycastResult)
        {
            var obj2 = (BodyRaycastResult)_highlightRaycastResult;
            ray = obj2.Ray;
            num = MathUtils.Min(obj2.Distance, 2f);
        }
        else if (_highlightRaycastResult is MovingBlocksRaycastResult)
        {
            var obj3 = (MovingBlocksRaycastResult)_highlightRaycastResult;
            ray = obj3.Ray;
            num = MathUtils.Min(obj3.Distance, 2f);
        }
        else
        {
            if (!(_highlightRaycastResult is Ray3))
            {
                return;
            }

            ray = (Ray3)_highlightRaycastResult;
            num = 2f;
        }

        var color = Color.White * 0.5f;
        var color2 = Color.Lerp(color, Color.Transparent, MathUtils.Saturate(num / 2f));
        var flatBatch3D = _primitivesRenderer3D.FlatBatch();
        flatBatch3D.QueueLine(ray.Position, ray.Position + ray.Direction * num, color, color2);
        flatBatch3D.Flush(camera.ViewProjectionMatrix);
    }

    public void DrawReticleHighlight(Camera camera)
    {
    }

    public void DrawFillHighlight(Camera camera)
    {
    }

    public void DrawOutlineHighlight(Camera camera)
    {
        if (camera.UsesMovementControls || !(_componentPlayer.ComponentHealth.Health > 0f) ||
            !_componentPlayer.ComponentGui.ControlsContainerWidget.IsVisible)
        {
            return;
        }

        if (_componentPlayer.ComponentMiner.DigCellFace.HasValue)
        {
            var value = _componentPlayer.ComponentMiner.DigCellFace.Value;
            var cellFaceBoundingBox = GetCellFaceBoundingBox(value.Point);
            var num = _subsystemSky.CalculateFog(camera.ViewPosition, cellFaceBoundingBox.Center());
            var color = Color.MultiplyNotSaturated(Color.Black, 1f - num);
            DrawBoundingBoxFace(_primitivesRenderer3D.FlatBatch(0, DepthStencilState.None), value.Face,
                cellFaceBoundingBox.Min, cellFaceBoundingBox.Max, color);
        }
        else
        {
            if (!_componentPlayer.ComponentAimingSights.IsSightsVisible &&
                (SettingsManager.LookControlMode == LookControlMode.SplitTouch ||
                 !_componentPlayer.ComponentInput.IsControlledByTouch) &&
                _highlightRaycastResult is TerrainRaycastResult)
            {
                var cellFace = ((TerrainRaycastResult)_highlightRaycastResult).CellFace;
                var cellFaceBoundingBox2 = GetCellFaceBoundingBox(cellFace.Point);
                var num2 = _subsystemSky.CalculateFog(camera.ViewPosition, cellFaceBoundingBox2.Center());
                var color2 = Color.MultiplyNotSaturated(Color.Black, 1f - num2);
                DrawBoundingBoxFace(_primitivesRenderer3D.FlatBatch(0, DepthStencilState.None), cellFace.Face,
                    cellFaceBoundingBox2.Min, cellFaceBoundingBox2.Max, color2);
            }

            if (NearbyEditableCell.HasValue)
            {
                var cellFaceBoundingBox3 = GetCellFaceBoundingBox(NearbyEditableCell.Value);
                var num3 = _subsystemSky.CalculateFog(camera.ViewPosition, cellFaceBoundingBox3.Center());
                var color3 = Color.MultiplyNotSaturated(Color.Black, 1f - num3);
                _primitivesRenderer3D.FlatBatch(0, DepthStencilState.None)
                    .QueueBoundingBox(cellFaceBoundingBox3, color3);
            }
        }

        _primitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
    }

    public static void DrawBoundingBoxFace(FlatBatch3D batch, int face, Vector3 c1, Vector3 c2, Color color)
    {
        switch (face)
        {
            case 0:
                batch.QueueLine(new Vector3(c1.X, c1.Y, c2.Z), new Vector3(c2.X, c1.Y, c2.Z), color);
                batch.QueueLine(new Vector3(c2.X, c2.Y, c2.Z), new Vector3(c1.X, c2.Y, c2.Z), color);
                batch.QueueLine(new Vector3(c2.X, c1.Y, c2.Z), new Vector3(c2.X, c2.Y, c2.Z), color);
                batch.QueueLine(new Vector3(c1.X, c2.Y, c2.Z), new Vector3(c1.X, c1.Y, c2.Z), color);
                break;
            case 1:
                batch.QueueLine(new Vector3(c2.X, c1.Y, c2.Z), new Vector3(c2.X, c2.Y, c2.Z), color);
                batch.QueueLine(new Vector3(c2.X, c1.Y, c1.Z), new Vector3(c2.X, c2.Y, c1.Z), color);
                batch.QueueLine(new Vector3(c2.X, c2.Y, c1.Z), new Vector3(c2.X, c2.Y, c2.Z), color);
                batch.QueueLine(new Vector3(c2.X, c1.Y, c1.Z), new Vector3(c2.X, c1.Y, c2.Z), color);
                break;
            case 2:
                batch.QueueLine(new Vector3(c1.X, c1.Y, c1.Z), new Vector3(c2.X, c1.Y, c1.Z), color);
                batch.QueueLine(new Vector3(c2.X, c1.Y, c1.Z), new Vector3(c2.X, c2.Y, c1.Z), color);
                batch.QueueLine(new Vector3(c2.X, c2.Y, c1.Z), new Vector3(c1.X, c2.Y, c1.Z), color);
                batch.QueueLine(new Vector3(c1.X, c2.Y, c1.Z), new Vector3(c1.X, c1.Y, c1.Z), color);
                break;
            case 3:
                batch.QueueLine(new Vector3(c1.X, c2.Y, c2.Z), new Vector3(c1.X, c1.Y, c2.Z), color);
                batch.QueueLine(new Vector3(c1.X, c2.Y, c1.Z), new Vector3(c1.X, c1.Y, c1.Z), color);
                batch.QueueLine(new Vector3(c1.X, c1.Y, c1.Z), new Vector3(c1.X, c1.Y, c2.Z), color);
                batch.QueueLine(new Vector3(c1.X, c2.Y, c1.Z), new Vector3(c1.X, c2.Y, c2.Z), color);
                break;
            case 4:
                batch.QueueLine(new Vector3(c2.X, c2.Y, c2.Z), new Vector3(c1.X, c2.Y, c2.Z), color);
                batch.QueueLine(new Vector3(c2.X, c2.Y, c1.Z), new Vector3(c1.X, c2.Y, c1.Z), color);
                batch.QueueLine(new Vector3(c1.X, c2.Y, c1.Z), new Vector3(c1.X, c2.Y, c2.Z), color);
                batch.QueueLine(new Vector3(c2.X, c2.Y, c1.Z), new Vector3(c2.X, c2.Y, c2.Z), color);
                break;
            case 5:
                batch.QueueLine(new Vector3(c1.X, c1.Y, c2.Z), new Vector3(c2.X, c1.Y, c2.Z), color);
                batch.QueueLine(new Vector3(c1.X, c1.Y, c1.Z), new Vector3(c2.X, c1.Y, c1.Z), color);
                batch.QueueLine(new Vector3(c1.X, c1.Y, c1.Z), new Vector3(c1.X, c1.Y, c2.Z), color);
                batch.QueueLine(new Vector3(c2.X, c1.Y, c1.Z), new Vector3(c2.X, c1.Y, c2.Z), color);
                break;
        }
    }

    public BoundingBox GetCellFaceBoundingBox(Point3 point)
    {
        var cellValue = _subsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
        var customCollisionBoxes = BlocksManager.Blocks[Terrain.ExtractContents(cellValue)]
            .GetCustomCollisionBoxes(_subsystemTerrain, cellValue);
        var vector = new Vector3(point.X, point.Y, point.Z);
        if (customCollisionBoxes.Length == 0)
        {
            return new BoundingBox(vector, vector + Vector3.One);
        }

        var boundingBox = customCollisionBoxes
            .Where(box => box != default)
            .Aggregate<BoundingBox, BoundingBox?>(null, (current, box) => current.HasValue
                ? BoundingBox.Union(current.Value, box)
                : box) ?? new BoundingBox(Vector3.Zero, Vector3.One);

        return new BoundingBox(boundingBox.Min + vector, boundingBox.Max + vector);
    }
}
