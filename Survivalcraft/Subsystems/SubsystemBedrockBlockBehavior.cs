using Engine.Graphics;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Subsystems;

public class Territoriy
{
    private const int _height = 32; //范围高度

    private const int _rangeFix = 16; //显示范围修正

    public readonly bool AllowBlockBehavior = true;

    public bool AllowDig;

    public bool AllowPlace;

    public bool ApplyToFriend;

    public BoundingBox BoundBox;

    public bool IsVisible;

    public Point2 Origin;

    public Point3 OwnChunkCoord;

    public Guid OwnerGuid;

    public BoundingBox ViewBoundBox;

    public void CalculateBox(int territoriySize)
    {
        BoundBox = new BoundingBox(
            new Vector3(Origin.X - 16 * territoriySize, OwnChunkCoord.Y, Origin.Y - 16 * territoriySize),
            new Vector3(Origin.X + 16 * territoriySize + 1, OwnChunkCoord.Y + _height,
                Origin.Y + 16 * territoriySize + 1));
        ViewBoundBox = new BoundingBox(
            new Vector3(Origin.X - 16 * territoriySize - _rangeFix, OwnChunkCoord.Y,
                Origin.Y - 16 * territoriySize - _rangeFix),
            new Vector3(Origin.X + 16 * territoriySize + _rangeFix + 1, OwnChunkCoord.Y + _height,
                Origin.Y + 16 * territoriySize + _rangeFix + 1));
    }
}

public class SubsystemBedrockBlockBehavior : SubsystemBlockBehavior, IDrawable
{
    public const int TerritoriySize = 1;

    //所有的领地
    public static readonly Dictionary<Guid, Territoriy> Territories = new();

    private DrawText? _drawText;

    private SubsystemModelsRenderer _subsystemModelsRenderer = null!;

    private readonly PrimitivesRenderer3D _primitives = new();

    public override int[] HandledBlocks => [1];

    public int[] DrawOrders => [2];

    public void Draw(Camera camera, int drawOrder)
    {
        foreach (var c in Territories.Values)
        {
            var drawFlag = true;
            if (c.OwnerGuid == camera.GameWidget.PlayerData.PlayerGUID)
            {
                drawFlag = c.IsVisible;
            }

            if (!drawFlag || !c.ViewBoundBox.Contains(camera.ViewPosition))
            {
                continue;
            }

            var flatBatch = _primitives.FlatBatch();
            flatBatch.QueueBoundingBox(c.BoundBox,
                c.OwnerGuid == camera.GameWidget.PlayerData.PlayerGUID ? Color.Green : Color.Yellow);
            _primitives.Flush(camera.ViewProjectionMatrix);
            break;
        }
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        if (Terrain.ExtractData(raycastResult.Value) == 0)
        {
            return false;
        }

        if (_drawText != null)
        {
            _subsystemModelsRenderer.RemoveDrawText(_drawText);
            _drawText = null;
        }

        if ((Terrain.ExtractData(raycastResult.Value) & 0x1) != 1)
        {
            return base.OnInteract(raycastResult, componentMiner);
        }

        if (CheckIsInTerritoriy(raycastResult.CellFace.X, raycastResult.CellFace.Z, out Territoriy? territoriy))
        {
            if (territoriy != null && componentMiner.ComponentPlayer != null && territoriy.OwnerGuid == componentMiner.ComponentPlayer.PlayerData.PlayerGUID)
            {
                var canvasWidget = new CanvasWidget
                    { Size = new Vector2(360, 180), HorizontalAlignment = WidgetAlignment.Near };
                var rectangle = new RectangleWidget
                    { FillColor = Color.Black, OutlineColor = Color.White, OutlineThickness = 2f };
                var stackPanel = new StackPanelWidget
                {
                    Margin = new Vector2(10, 0), HorizontalAlignment = WidgetAlignment.Near,
                    VerticalAlignment = WidgetAlignment.Center, Direction = LayoutDirection.Vertical
                };
                var show = new CheckboxWidget { Text = "显示范围", Size = new Vector2(36f) };
                var applyToFriend = new CheckboxWidget { Text = "队友开放", Size = new Vector2(36f) };
                territoriy.AllowDig = false;
                territoriy.AllowPlace = false;
                show.CheckStatusChanged += flag =>
                {
                    territoriy.IsVisible = flag;
                    CommonLib.Net.QueuePackage(new TerritoriyPackage(territoriy));
                };
                applyToFriend.CheckStatusChanged += flag =>
                {
                    territoriy.ApplyToFriend = flag;
                    CommonLib.Net.QueuePackage(new TerritoriyPackage(territoriy));
                };
                show.IsChecked = territoriy.IsVisible;
                applyToFriend.IsChecked = territoriy.ApplyToFriend;
                canvasWidget.Children.Add(rectangle);
                canvasWidget.Children.Add(stackPanel);
                stackPanel.Children.Add(show);
                stackPanel.Children.Add(applyToFriend);
                componentMiner.ComponentPlayer.ComponentGui.ModalPanelWidget =
                    componentMiner.ComponentPlayer.ComponentGui.ModalPanelWidget == null ? canvasWidget : null;
            }
            else
            {
                componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage("你没有该领地石使用权限", Color.Yellow, false,
                    true);
            }
        }
        else
        {
            componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage("该领地石未生效", Color.Yellow, false, true);
        }

        return base.OnInteract(raycastResult, componentMiner);
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        var value = SubsystemTerrain.Terrain.GetCellValue(cellFace.Point.X, cellFace.Point.Y, cellFace.Point.Z);
        if (Terrain.ExtractData(value) == 0)
        {
            return;
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        var v = worldItem.Value;
        if (v == 0)
        {
            return;
        }

        var b = BlocksManager.Blocks[Terrain.ExtractContents(v)];
        var data = Terrain.ExtractData(v);
        var tmp = $"{b.CraftingId}:{data}";
        var t = $"禁用方块ID {tmp}";
        if (_drawText == null)
        {
            _drawText = _subsystemModelsRenderer.AddDrawText(
                new Vector3(cellFace.Point) + new Vector3(0.5f),
                t,
                Color.White
            );
        }
        else
        {
            _drawText.Text = t;
        }
    }

    public static bool CheckIsInTerritoriy(int x, int z, out Guid owner)
    {
        foreach (var (key, territoriy) in Territories)
        {
            if (x < territoriy.Origin.X - 16 * TerritoriySize || z < territoriy.Origin.Y - 16 * TerritoriySize ||
                x > territoriy.Origin.X + 16 * TerritoriySize ||
                z > territoriy.Origin.Y + 16 * TerritoriySize)
            {
                continue;
            }

            owner = key;
            return true;
        }

        owner = Guid.Empty;
        return false;
    }

    public static bool CheckIsInTerritoriy(int x, int z, out Territoriy? territoriy)
    {
        foreach (var item in Territories)
        {
            territoriy = item.Value;
            if (x >= territoriy.Origin.X - 16 * TerritoriySize && z >= territoriy.Origin.Y - 16 * TerritoriySize &&
                x <= territoriy.Origin.X + 16 * TerritoriySize &&
                z <= territoriy.Origin.Y + 16 * TerritoriySize)
            {
                return true;
            }
        }

        territoriy = null;
        return false;
    }

    public static bool CheckIsInTerritoriyInnerCircle(int x, int z, out Territoriy? territoriy)
    {
        foreach (var item in Territories)
        {
            territoriy = item.Value;
            if (x >= territoriy.Origin.X - 16 * TerritoriySize + 1 &&
                z >= territoriy.Origin.Y - 16 * TerritoriySize + 1 &&
                x <= territoriy.Origin.X + 16 * TerritoriySize - 1 &&
                z <= territoriy.Origin.Y + 16 * TerritoriySize - 1)
            {
                return true;
            }
        }

        territoriy = null;
        return false;
    }

    public static bool CheckIsInTerritoriyBorder(int x, int z, out Territoriy? territoriy)
    {
        foreach (var item in Territories)
        {
            territoriy = item.Value;
            if (x < territoriy.Origin.X - 16 * TerritoriySize || z < territoriy.Origin.Y - 16 * TerritoriySize ||
                x > territoriy.Origin.X + 16 * TerritoriySize ||
                z > territoriy.Origin.Y + 16 * TerritoriySize)
            {
                continue;
            }

            if (!(x >= territoriy.Origin.X - 16 * TerritoriySize + 1 &&
                  z >= territoriy.Origin.Y - 16 * TerritoriySize + 1 &&
                  x <= territoriy.Origin.X + 16 * TerritoriySize - 1 &&
                  z <= territoriy.Origin.Y + 16 * TerritoriySize - 1))
            {
                return true;
            }
        }

        territoriy = null;
        return false;
    }

    public static bool IsInTerritoriyBorder(Territoriy territoriy, int x, int z)
    {
        if (x < territoriy.Origin.X - 16 * TerritoriySize || z < territoriy.Origin.Y - 16 * TerritoriySize ||
            x > territoriy.Origin.X + 16 * TerritoriySize ||
            z > territoriy.Origin.Y + 16 * TerritoriySize)
        {
            return false;
        }

        return !(x >= territoriy.Origin.X - 16 * TerritoriySize + 1 &&
                 z >= territoriy.Origin.Y - 16 * TerritoriySize + 1 &&
                 x <= territoriy.Origin.X + 16 * TerritoriySize - 1 &&
                 z <= territoriy.Origin.Y + 16 * TerritoriySize - 1);
    }

    public static bool AllowPlayerAction(ComponentPlayer? componentPlayer, Territoriy territoriy)
    {
        if (componentPlayer == null)
        {
            return true;
        }

        //在队伍里面
        var isInGroup = componentPlayer.PlayerData.IsInGroup(territoriy.OwnerGuid);
        //是所有者
        var isSelf = componentPlayer.PlayerData.PlayerGUID == territoriy.OwnerGuid;
        //是服管理
        var isAdmin = componentPlayer.PlayerData.ServerManager || componentPlayer.PlayerData.ServerMaster;
        return isSelf || isAdmin || (isInGroup && territoriy.ApplyToFriend);
    }

    public override void OnBlockPlaced(ComponentMiner miner, int x, int y, int z, ref BlockPlacementData placementData,
        int itemValue)
    {
        var data = Terrain.ExtractData(itemValue);
        var player = miner.ComponentPlayer;
        var origin = Terrain.ToChunk(x, z) * 16;
        if (data != 1)
        {
            return;
        }

        if (player != null && Territories.TryGetValue(player.PlayerData.PlayerGUID, out var territoriy))
        {
            return;
        }

        if (player == null)
        {
            return;
        }

        territoriy = new Territoriy
        {
            IsVisible = true,
            AllowDig = true,
            AllowPlace = true,
            OwnerGuid = player.PlayerData.PlayerGUID,
            OwnChunkCoord = new Point3(x, y, z),
            Origin = origin
        };

        territoriy.CalculateBox(TerritoriySize);
        //检测是否会碰撞
        foreach (var item in Territories)
        {
            if (territoriy.BoundBox.Intersection(item.Value.BoundBox))
            {
                player.ComponentGui.DisplaySmallMessage("范围内有其它玩家领地，生效失败!", Color.Red, false,
                    true);
                return;
            }
        }

        Territories.Add(player.PlayerData.PlayerGUID, territoriy);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        if (Terrain.ExtractData(value) == 0)
        {
            return;
        }

        if (CheckIsInTerritoriy(x, z, out Territoriy? territoriy) && territoriy is not null)
        {
            Territories.Remove(territoriy.OwnerGuid);
        }

        if (_drawText == null)
        {
            return;
        }

        _subsystemModelsRenderer.RemoveDrawText(_drawText);
        _drawText = null;
    }

    public override void Dispose()
    {
        Territories.Clear();
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemModelsRenderer = Project.FindSubsystem<SubsystemModelsRenderer>(true)!;
        foreach (ValuesDictionary items in valuesDictionary.GetValue<ValuesDictionary>("Territoriy").Values)
        {
            var territoriy = new Territoriy
            {
                AllowDig = items.GetValue<bool>("AllowDig"),
                AllowPlace = items.GetValue<bool>("AllowPlace"),
                OwnChunkCoord = items.GetValue<Point3>("OwnChunkCoord"),
                IsVisible = items.GetValue<bool>("IsVisible"),
                ApplyToFriend = items.GetValue<bool>("ApplyToFirend"),
                OwnerGuid = items.GetValue<Guid>("Guid")
            };
            territoriy.Origin = Terrain.ToChunk(territoriy.OwnChunkCoord.X, territoriy.OwnChunkCoord.Z) * 16;
            territoriy.CalculateBox(TerritoriySize);
            Territories.Add(items.GetValue<Guid>("Guid"), territoriy);
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var keys = new ValuesDictionary();
        var i = 0;
        foreach (var item in Territories)
        {
            var valuePairs = new ValuesDictionary();
            valuePairs.SetValue("Guid", item.Key);
            valuePairs.SetValue("OwnChunkCoord", item.Value.OwnChunkCoord);
            valuePairs.SetValue("AllowDig", item.Value.AllowDig);
            valuePairs.SetValue("AllowPlace", item.Value.AllowPlace);
            valuePairs.SetValue("AllowBlockBehavior", item.Value.AllowBlockBehavior);
            valuePairs.SetValue("ApplyToFirend", item.Value.ApplyToFriend);
            valuePairs.SetValue("IsVisible", item.Value.IsVisible);
            keys.SetValue(i++.ToString(), valuePairs);
        }

        valuesDictionary.SetValue("Territoriy", keys);
    }
}
