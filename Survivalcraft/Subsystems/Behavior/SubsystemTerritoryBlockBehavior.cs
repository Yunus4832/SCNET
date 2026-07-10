using Engine.Graphics;

using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class Territoriy
{
    /// <summary>
    /// 范围高度
    /// </summary>
    private const int _height = 32;

    /// <summary>
    /// 显示范围修正
    /// </summary>
    private const int _rangeFix = 16;

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

public class SubsystemTerritoryBlockBehavior : SubsystemBlockBehavior, IDrawable
{
    private const string _typeName = nameof(SubsystemTerritoryBlockBehavior);

    public const int TerritoriySize = 1;

    /// <summary>
    /// 所有的领地
    /// </summary>
    public static readonly Dictionary<Guid, Territoriy> Territories = new();

    private DrawText? _drawText;

    private SubsystemModelsRenderer _subsystemModelsRenderer = null!;

    private readonly PrimitivesRenderer3D _primitives = new();

    public override int[] HandledBlocks => [TerritoryBlock.Index];

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
        if (!TerritoryBlock.IsTerritoryValue(raycastResult.Value))
        {
            return false;
        }

        if (_drawText != null)
        {
            _subsystemModelsRenderer.RemoveDrawText(_drawText);
            _drawText = null;
        }

        if (CheckIsInTerritoriy(raycastResult.CellFace.X, raycastResult.CellFace.Z, out Territoriy? territoriy))
        {
            if (territoriy != null && componentMiner.ComponentPlayer != null &&
                territoriy.OwnerGuid == componentMiner.ComponentPlayer.PlayerData.PlayerGUID)
            {
                territoriy.AllowDig = false;
                territoriy.AllowPlace = false;
                var canvasWidget = new TerritorySettingsWidget(
                    territoriy,
                    () => componentMiner.ComponentPlayer.ComponentGui.ModalPanelWidget = null);
                componentMiner.ComponentPlayer.ComponentGui.ModalPanelWidget =
                    componentMiner.ComponentPlayer.ComponentGui.ModalPanelWidget == null ? canvasWidget : null;
            }
            else
            {
                componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                    LanguageManager.Get(_typeName, 3),
                    Color.Yellow,
                    false,
                    true);
            }
        }
        else
        {
            componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                LanguageManager.Get(_typeName, 4),
                Color.Yellow,
                false,
                true);
        }

        return base.OnInteract(raycastResult, componentMiner);
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        var value = SubsystemTerrain.Terrain.GetCellValue(cellFace.Point.X, cellFace.Point.Y, cellFace.Point.Z);
        if (!TerritoryBlock.IsTerritoryValue(value))
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
        var t = string.Format(LanguageManager.Get(_typeName, 5), tmp);
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

        // 在队伍里面
        var isInGroup = componentPlayer.PlayerData.IsInGroup(territoriy.OwnerGuid);
        // 是所有者
        var isSelf = componentPlayer.PlayerData.PlayerGUID == territoriy.OwnerGuid;
        // 是服管理
        var isAdmin = componentPlayer.PlayerData.ServerManager || componentPlayer.PlayerData.ServerMaster;
        return isSelf || isAdmin || (isInGroup && territoriy.ApplyToFriend);
    }

    public override void OnBlockPlaced(ComponentMiner miner, int x, int y, int z, ref BlockPlacementData placementData,
        int itemValue)
    {
        var player = miner.ComponentPlayer;
        var origin = Terrain.ToChunk(x, z) * 16;
        if (!TerritoryBlock.IsTerritoryValue(itemValue))
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

        // 检测是否会碰撞
        if (Territories.Any(item => territoriy.BoundBox.Intersection(item.Value.BoundBox)))
        {
            player.ComponentGui.DisplaySmallMessage(
                LanguageManager.Get(_typeName, 6),
                Color.Red,
                false,
                true
            );
            return;
        }

        Territories.Add(player.PlayerData.PlayerGUID, territoriy);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        if (!TerritoryBlock.IsTerritoryValue(value))
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
                ApplyToFriend = items.GetValue<bool>("ApplyToFriend"),
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
            valuePairs.SetValue("ApplyToFriend", item.Value.ApplyToFriend);
            valuePairs.SetValue("IsVisible", item.Value.IsVisible);
            keys.SetValue(i++.ToString(), valuePairs);
        }

        valuesDictionary.SetValue("Territoriy", keys);
    }

    private sealed class TerritorySettingsWidget : CanvasWidget
    {
        private readonly Action _close;

        private bool _isFirstUpdate = true;

        public TerritorySettingsWidget(Territoriy territoriy, Action close)
        {
            _close = close;
            Size = new Vector2(360, 180);
            HorizontalAlignment = WidgetAlignment.Near;

            Children.Add(new RectangleWidget
            {
                FillColor = Color.Black,
                OutlineColor = Color.White,
                OutlineThickness = 2f
            });

            var stackPanel = new StackPanelWidget
            {
                Margin = new Vector2(10, 0),
                HorizontalAlignment = WidgetAlignment.Near,
                VerticalAlignment = WidgetAlignment.Center,
                Direction = LayoutDirection.Vertical
            };
            Children.Add(stackPanel);

            var show = new CheckboxWidget
            {
                Text = LanguageManager.Get(_typeName, 1),
                Size = new Vector2(36f),
                IsChecked = territoriy.IsVisible
            };
            show.CheckStatusChanged += flag =>
            {
                territoriy.IsVisible = flag;
                CommonLib.Net.QueuePackage(new TerritoriyPackage(territoriy));
            };
            stackPanel.Children.Add(show);

            var applyToFriend = new CheckboxWidget
            {
                Text = LanguageManager.Get(_typeName, 2),
                Size = new Vector2(36f),
                IsChecked = territoriy.ApplyToFriend
            };
            applyToFriend.CheckStatusChanged += flag =>
            {
                territoriy.ApplyToFriend = flag;
                CommonLib.Net.QueuePackage(new TerritoriyPackage(territoriy));
            };
            stackPanel.Children.Add(applyToFriend);
        }

        public override void Update()
        {
            if (_isFirstUpdate)
            {
                _isFirstUpdate = false;
                return;
            }

            var click = Input.Click;
            if (click.HasValue &&
                !GlobalBounds.Contains(click.Value.Start) &&
                !GlobalBounds.Contains(click.Value.End))
            {
                _close();
            }
        }
    }
}
