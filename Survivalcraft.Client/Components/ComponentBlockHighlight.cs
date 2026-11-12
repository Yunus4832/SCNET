using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentBlockHighlight : Component, IDrawable, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    private static readonly int[] _drawOrders = [1, 2000];

    private ComponentCharacter _componentCharacter = null!;

    private object? _highlightRaycastResult;

    private PrimitivesRenderer3D PrimitivesRenderer3D => field ??= new PrimitivesRenderer3D();

    private SubsystemSkyPresentation _subsystemSky = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    public Point3? NearbyEditableCell { get; set; }

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        if (camera.GameWidget.CharacterData != _componentCharacter.CharacterData)
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
    }

    public UpdateOrder UpdateOrder => UpdateOrder.BlockHighlight;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        var activeCamera = _componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget
            .ActiveCamera;
        var ray = new Ray3(activeCamera.ViewPosition, activeCamera.ViewDirection);
        NearbyEditableCell = null;
        _highlightRaycastResult = _componentCharacter.ComponentMiner.Raycast(ray, RaycastMode.Digging);
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
        var obj = Modding.Blocks.WorldContentBinding.For(Project).Catalog
            .Blocks[Terrain.ExtractContents(cellValue)];
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

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSkyPresentation>(true)!;
        _componentCharacter = Entity.FindComponent<ComponentCharacter>(true)!;
    }

    public void DrawRayHighlight(Camera camera)
    {
        Ray3 ray;
        float num;
        switch (_highlightRaycastResult)
        {
            case TerrainRaycastResult result:
                ray = result.Ray;
                num = MathUtils.Min(result.Distance, 2f);
                break;
            case BodyRaycastResult obj2:
                ray = obj2.Ray;
                num = MathUtils.Min(obj2.Distance, 2f);
                break;
            case MovingBlocksRaycastResult obj3:
                ray = obj3.Ray;
                num = MathUtils.Min(obj3.Distance, 2f);
                break;
            default:
            {
                if (_highlightRaycastResult is not Ray3 ray3)
                {
                    return;
                }

                ray = ray3;
                num = 2f;
                break;
            }
        }

        var color = Color.White * 0.5f;
        var color2 = Color.Lerp(color, Color.Transparent, MathUtils.Saturate(num / 2f));
        var flatBatch3D = PrimitivesRenderer3D.FlatBatch();
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
        if (camera.UsesMovementControls ||
            !(_componentCharacter.ComponentHealth.Health > 0f) ||
            !_componentCharacter.Entity.FindComponent<ComponentGui>(true)!.ControlsContainerWidget.IsVisible)
        {
            return;
        }

        if (_componentCharacter.ComponentMiner.DigCellFace.HasValue)
        {
            var value = _componentCharacter.ComponentMiner.DigCellFace.Value;
            var cellFaceBoundingBox = GetCellFaceBoundingBox(value.Point);
            var num = _subsystemSky.CalculateFog(camera.ViewPosition, cellFaceBoundingBox.Center());
            var color = Color.MultiplyNotSaturated(Color.Black, 1f - num);
            DrawBoundingBoxFace(
                PrimitivesRenderer3D.FlatBatch(0, DepthStencilState.None),
                value.Face,
                cellFaceBoundingBox.Min,
                cellFaceBoundingBox.Max,
                color
            );
        }
        else
        {
            if (!_componentCharacter.Entity.FindComponent<ComponentAimingSights>(true)!.IsSightsVisible &&
                (Configuration.ClientConfiguration.Preferences.LookControlMode == LookControlMode.SplitTouch ||
                 !_componentCharacter.Entity.FindComponent<ComponentInput>(true)!.IsTouchInputActive) &&
                _highlightRaycastResult is TerrainRaycastResult result)
            {
                var cellFace = result.CellFace;
                var cellFaceBoundingBox2 = GetCellFaceBoundingBox(cellFace.Point);
                var num2 = _subsystemSky.CalculateFog(camera.ViewPosition, cellFaceBoundingBox2.Center());
                var color2 = Color.MultiplyNotSaturated(Color.Black, 1f - num2);
                DrawBoundingBoxFace(
                    PrimitivesRenderer3D.FlatBatch(0, DepthStencilState.None),
                    cellFace.Face,
                    cellFaceBoundingBox2.Min,
                    cellFaceBoundingBox2.Max,
                    color2
                );
            }

            if (NearbyEditableCell.HasValue)
            {
                var cellFaceBoundingBox3 = GetCellFaceBoundingBox(NearbyEditableCell.Value);
                var num3 = _subsystemSky.CalculateFog(camera.ViewPosition, cellFaceBoundingBox3.Center());
                var color3 = Color.MultiplyNotSaturated(Color.Black, 1f - num3);
                PrimitivesRenderer3D.FlatBatch(0, DepthStencilState.None)
                    .QueueBoundingBox(cellFaceBoundingBox3, color3);
            }
        }

        PrimitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
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
        var customCollisionBoxes = Modding.Blocks.WorldContentBinding.For(Project).Catalog
            .Blocks[Terrain.ExtractContents(cellValue)]
            .GetCustomCollisionBoxes(_subsystemTerrain, cellValue);
        var vector = new Vector3(point.X, point.Y, point.Z);
        if (customCollisionBoxes.Length == 0)
        {
            return new BoundingBox(vector, vector + Vector3.One);
        }

        var boundingBox = customCollisionBoxes
                              .Where(box => box != default)
                              .Aggregate<BoundingBox, BoundingBox?>(
                                  null,
                                  (current, box) => current.HasValue
                                      ? BoundingBox.Union(current.Value, box)
                                      : box
                              ) ??
                          new BoundingBox(Vector3.Zero, Vector3.One);

        return new BoundingBox(boundingBox.Min + vector, boundingBox.Max + vector);
    }
}
