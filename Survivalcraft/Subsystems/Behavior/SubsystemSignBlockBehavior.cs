using System.Globalization;

using Engine.Graphics;
using Engine.Media;

using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemSignBlockBehavior : SubsystemBlockBehavior, IDrawable, IUpdateable
{
    public const float MaxVisibilityDistanceSqr = 400f;

    public const float MinUpdateDistance = 2f;

    public const int TextWidth = 128;

    public const int TextHeight = 32;

    public const int MaxTexts = 32;

    private static readonly int[] _drawOrders = [50];

    public bool CopySignsText;

#if SERVER
    private readonly BitmapFont _font = null!;
#else
    private readonly BitmapFont _font = LabelWidget.BitmapFont;
#endif

    private readonly List<Vector3> _lastUpdatePositions = [];

    private readonly List<TextData> _nearTexts = [];

    private readonly PrimitivesRenderer2D _primitivesRenderer2D = new();

    private readonly PrimitivesRenderer3D _primitivesRenderer3D = new();

    private RenderTarget2D RenderTarget
    {
        get => field is not null ? field : throw new InvalidOperationException("RenderTarget  is not initialized");
        set;
    } = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemGameWidgets _subsystemViews = null!;

    private readonly Dictionary<Point3, TextData> _textsByPoint = new();

    private readonly TextData?[] _textureLocations = new TextData[32];

    private readonly List<RenderTarget2D> _texturesByPoint = [];

    public bool ShowSignsTexture;

    public override int[] HandledBlocks =>
    [
        23,
        97,
        98,
        210,
        211
    ];

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
#if SERVER
        return;
#else
        DrawSigns(camera);
#endif
    }


    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
#if SERVER
        return;
#else
        UpdateRenderTarget();
#endif
    }

    public SignData? GetSignData(Point3 point)
    {
        //关键词屏蔽
        var arr = Project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.KeywordBlocking
            .Split([';'], StringSplitOptions.None);
        if (!_textsByPoint.TryGetValue(point, out var value))
        {
            return null;
        }

        for (var i = 0; i < value.Lines.Length; i++)
        {
            foreach (var k in arr)
            {
                if (!string.IsNullOrEmpty(k))
                {
                    value.Lines[i] = value.Lines[i].Replace(k, "*");
                }
            }
        }

        return new SignData
        {
            Lines = value.Lines.ToArray(),
            Colors = value.Colors.ToArray(),
            Url = value.Url
        };
    }

    public void SetSignData(Point3 point, string[] lines, Color[] colors, string url)
    {
        var textData = new TextData
        {
            Point = point
        };
        for (var i = 0; i < 4; i++)
        {
            textData.Lines[i] = lines[i];
            textData.Colors[i] = colors[i];
        }

        textData.Url = url;
        _textsByPoint[point] = textData;
        _lastUpdatePositions.Clear();
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellValueFast = SubsystemTerrain.Terrain.GetCellValueFast(x, y, z);
        var num = Terrain.ExtractContents(cellValueFast);
        var data = Terrain.ExtractData(cellValueFast);
        var block = BlocksManager.Blocks[num];
        if (block is AttachedSignBlock)
        {
            var point = CellFace.FaceToPoint3(AttachedSignBlock.GetFace(data));
            var x2 = x - point.X;
            var y2 = y - point.Y;
            var z2 = z - point.Z;
            var cellValue = SubsystemTerrain.Terrain.GetCellValue(x2, y2, z2);
            var cellContents = Terrain.ExtractContents(cellValue);
            if (!BlocksManager.Blocks[cellContents].IsCollidable(cellValue))
            {
                SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
            }
        }
        else if (block is PostedSignBlock)
        {
            var num2 = PostedSignBlock.GetHanging(data)
                ? SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z)
                : SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);

            if (!BlocksManager.Blocks[Terrain.ExtractContents(num2)].IsCollidable(num2))
            {
                SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
            }
        }
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        var point = new Point3(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);
        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Adventure)
        {
            var signData = GetSignData(point);
            if (signData != null && !string.IsNullOrEmpty(signData.Url))
            {
                WebBrowserManager.LaunchBrowser(signData.Url);
            }
        }
        else
        {
            if (CommonLib.WorkType == WorkType.Client && componentMiner.ComponentPlayer == CommonLib.MainPlayer)
            {
                IPackage package =
                    new BlockEditPackage(
                        new Point3(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z),
                        BlockEditPackage.EventType.EditSign);
                CommonLib.Net.QueuePackage(package);
                AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                return true;
            }

            if (componentMiner.ComponentPlayer is { PlayerData.IsMainPlayer: false })
            {
                return true;
            }

            DialogsManager.ShowDialog(componentMiner.ComponentPlayer?.GuiWidget, new EditSignDialog(this, point));
            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        }

        return true;
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        var key = new Point3(x, y, z);
        _textsByPoint.Remove(key);
        _lastUpdatePositions.Clear();
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
#if !SERVER
        _subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
#endif
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
#if !SERVER
        CreateRenderTarget();
#endif
        foreach (ValuesDictionary value11 in valuesDictionary.GetValue<ValuesDictionary>("Texts").Values)
        {
            var value = value11.GetValue<Point3>("Point");
            var value2 = value11.GetValue("Line1", string.Empty);
            var value3 = value11.GetValue("Line2", string.Empty);
            var value4 = value11.GetValue("Line3", string.Empty);
            var value5 = value11.GetValue("Line4", string.Empty);
            var value6 = value11.GetValue("Color1", Color.Black);
            var value7 = value11.GetValue("Color2", Color.Black);
            var value8 = value11.GetValue("Color3", Color.Black);
            var value9 = value11.GetValue("Color4", Color.Black);
            var value10 = value11.GetValue("Url", string.Empty);
            SetSignData(
                value,
                [
                    value2,
                    value3,
                    value4,
                    value5
                ],
                [
                    value6,
                    value7,
                    value8,
                    value9
                ],
                value10
            );
        }

#if !SERVER
        Display.DeviceReset += DisplayDeviceReset;
#endif
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var num = 0;
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Texts", valuesDictionary2);
        foreach (var value in _textsByPoint.Values)
        {
            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary3.SetValue("Point", value.Point);
            if (!string.IsNullOrEmpty(value.Lines[0]))
            {
                valuesDictionary3.SetValue("Line1", value.Lines[0]);
            }

            if (!string.IsNullOrEmpty(value.Lines[1]))
            {
                valuesDictionary3.SetValue("Line2", value.Lines[1]);
            }

            if (!string.IsNullOrEmpty(value.Lines[2]))
            {
                valuesDictionary3.SetValue("Line3", value.Lines[2]);
            }

            if (!string.IsNullOrEmpty(value.Lines[3]))
            {
                valuesDictionary3.SetValue("Line4", value.Lines[3]);
            }

            if (value.Colors[0] != Color.Black)
            {
                valuesDictionary3.SetValue("Color1", value.Colors[0]);
            }

            if (value.Colors[1] != Color.Black)
            {
                valuesDictionary3.SetValue("Color2", value.Colors[1]);
            }

            if (value.Colors[2] != Color.Black)
            {
                valuesDictionary3.SetValue("Color3", value.Colors[2]);
            }

            if (value.Colors[3] != Color.Black)
            {
                valuesDictionary3.SetValue("Color4", value.Colors[3]);
            }

            if (!string.IsNullOrEmpty(value.Url))
            {
                valuesDictionary3.SetValue("Url", value.Url);
            }

            valuesDictionary2.SetValue(num++.ToString(CultureInfo.InvariantCulture), valuesDictionary3);
        }
    }

    public override void Dispose()
    {
#if !SERVER
        var renderTarget2D = RenderTarget;
        Utilities.Dispose(ref renderTarget2D);
        Display.DeviceReset -= DisplayDeviceReset;
#endif
    }

    private void DisplayDeviceReset()
    {
        InvalidateRenderTarget();
    }

    private void CreateRenderTarget()
    {
        RenderTarget = new RenderTarget2D((int)_font.GlyphHeight * 16, (int)_font.GlyphHeight * 4 * 32, 1,
            ColorFormat.Rgba8888, DepthFormat.None);
    }

    private void InvalidateRenderTarget()
    {
        _lastUpdatePositions.Clear();
        for (var i = 0; i < _textureLocations.Length; i++)
        {
            _textureLocations[i] = null;
        }

        foreach (var value in _textsByPoint.Values)
        {
            value.TextureLocation = null;
        }
    }

    private void RenderText(FontBatch2D fontBatch, FlatBatch2D flatBatch, TextData textData)
    {
        if (!textData.TextureLocation.HasValue)
        {
            return;
        }

        var list = new List<string>();
        var list2 = new List<Color>();
        for (var i = 0; i < textData.Lines.Length; i++)
        {
            if (!string.IsNullOrEmpty(textData.Lines[i]))
            {
                list.Add(textData.Lines[i].Replace("\\", "").ToUpper());
                list2.Add(textData.Colors[i]);
            }
        }

        if (list.Count <= 0)
        {
            return;
        }

        var num = list.Max(l => l.Length) * _font.GlyphHeight;
        var num2 = list.Count * _font.GlyphHeight;
        var num3 = 4f;
        float num4;
        float num5;
        if (num / num2 < num3)
        {
            num4 = num2 * num3;
            num5 = num2;
        }
        else
        {
            num4 = num;
            num5 = num / num3;
        }

        var flag = !string.IsNullOrEmpty(textData.Url);
        for (var j = 0; j < list.Count; j++)
        {
            fontBatch.QueueText(
                position: new Vector2(num4 / 2f,
                    j * _font.GlyphHeight + textData.TextureLocation.Value * (4f * _font.GlyphHeight) +
                    (num5 - num2) / 2f), text: list[j], depth: 0f, color: flag ? new Color(0, 0, 64) : list2[j],
                anchor: TextAnchor.HorizontalCenter, scale: new Vector2(1f / _font.Scale), spacing: Vector2.Zero);
        }

        textData.UsedTextureWidth = num4;
        textData.UsedTextureHeight = num5;
    }

    private void UpdateRenderTarget()
    {
        var flag = false;
        foreach (var gameWidget in _subsystemViews.GameWidgets)
        {
            var flag2 = false;
            foreach (var lastUpdatePosition in _lastUpdatePositions)
            {
                if (Vector3.DistanceSquared(gameWidget.ActiveCamera.ViewPosition, lastUpdatePosition) < 4f)
                {
                    flag2 = true;
                    break;
                }
            }

            if (flag2)
            {
                continue;
            }

            flag = true;
            break;
        }

        if (!flag)
        {
            return;
        }

        _lastUpdatePositions.Clear();
        _lastUpdatePositions.AddRange(_subsystemViews.GameWidgets.Select(v => v.ActiveCamera.ViewPosition));
        _nearTexts.Clear();
        foreach (var value in _textsByPoint.Values)
        {
            var point = value.Point;
            var num = _subsystemViews.CalculateSquaredDistanceFromNearestView(new Vector3(point));
            if (!(num <= 400f))
            {
                continue;
            }

            value.Distance = num;
            _nearTexts.Add(value);
        }

        _nearTexts.Sort((d1, d2) => Comparer<float>.Default.Compare(d1.Distance, d2.Distance));
        if (_nearTexts.Count > 32)
        {
            _nearTexts.RemoveRange(32, _nearTexts.Count - 32);
        }

        foreach (var nearText in _nearTexts)
        {
            nearText.ToBeRenderedFrame = Time.FrameIndex;
        }

        var flag3 = false;
        for (var i = 0; i < MathUtils.Min(_nearTexts.Count, 32); i++)
        {
            var textData = _nearTexts[i];
            if (textData.TextureLocation.HasValue)
            {
                continue;
            }

            var num2 = _textureLocations.FirstIndex(d => d == null);
            if (num2 < 0)
            {
                num2 = _textureLocations.FirstIndex(d => d?.ToBeRenderedFrame != Time.FrameIndex);
            }

            if (num2 < 0)
            {
                continue;
            }

            var textData2 = _textureLocations[num2];
            if (textData2 != null)
            {
                textData2.TextureLocation = null;
                _textureLocations[num2] = null;
            }

            _textureLocations[num2] = textData;
            textData.TextureLocation = num2;
            flag3 = true;
        }

        if (!flag3)
        {
            return;
        }

        var renderTarget = Display.RenderTarget;
        Display.RenderTarget = RenderTarget;
        try
        {
            Display.Clear(new Vector4(Color.Transparent));
            var flatBatch = _primitivesRenderer2D.FlatBatch(0, DepthStencilState.None, null, BlendState.Opaque);
            var fontBatch = _primitivesRenderer2D.FontBatch(_font, 1, DepthStencilState.None, null, BlendState.Opaque,
                SamplerState.PointClamp);
            foreach (var textData3 in _textureLocations)
            {
                if (textData3 != null)
                {
                    RenderText(fontBatch, flatBatch, textData3);
                }
            }

            _primitivesRenderer2D.Flush();
        }
        finally
        {
            Display.RenderTarget = renderTarget;
        }
    }

    private void DrawSigns(Camera camera)
    {
        if (_nearTexts.Count <= 0)
        {
            return;
        }

        var texturedBatch3D = _primitivesRenderer3D.TexturedBatch(RenderTarget, false, 0,
            DepthStencilState.DepthRead, RasterizerState.CullCounterClockwiseScissor, null, SamplerState.PointClamp);
        foreach (var nearText in _nearTexts)
        {
            if (!nearText.TextureLocation.HasValue)
            {
                continue;
            }

            var cellValue =
                _subsystemTerrain.Terrain.GetCellValue(nearText.Point.X, nearText.Point.Y, nearText.Point.Z);
            var num = Terrain.ExtractContents(cellValue);
            if (BlocksManager.Blocks[num] is not SignBlock signBlock)
            {
                continue;
            }

            var data = Terrain.ExtractData(cellValue);
            var signSurfaceBlockMesh = signBlock.GetSignSurfaceBlockMesh(data);
            var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(nearText.Point.X, nearText.Point.Z, false);
            if (chunkAtCell is { State: >= TerrainChunkState.InvalidVertices1 })
            {
                nearText.Light = Terrain.ExtractLight(cellValue);
            }

            var num2 = LightingManager.LightIntensityByLightValue[nearText.Light];
            var color = new Color(num2, num2, num2);
            var x = 0f;
            var x2 = nearText.UsedTextureWidth / (_font.GlyphHeight * 16f);
            var x3 = nearText.TextureLocation.Value / 32f;
            var x4 = (nearText.TextureLocation.Value + nearText.UsedTextureHeight / (_font.GlyphHeight * 4f)) /
                     32f;
            var signSurfaceNormal = signBlock.GetSignSurfaceNormal(data);
            var vector = new Vector3(nearText.Point.X, nearText.Point.Y, nearText.Point.Z);
            var num3 = Vector3.Dot(camera.ViewPosition - (vector + new Vector3(0.5f)), signSurfaceNormal);
            var vector2 = MathUtils.Max(0.01f * num3, 0.005f) * signSurfaceNormal;
            for (var i = 0; i < signSurfaceBlockMesh.Indices.Count / 3; i++)
            {
                var blockMeshVertex =
                    signSurfaceBlockMesh.Vertices.Array[signSurfaceBlockMesh.Indices.Array[i * 3]];
                var blockMeshVertex2 =
                    signSurfaceBlockMesh.Vertices.Array[signSurfaceBlockMesh.Indices.Array[i * 3 + 1]];
                var blockMeshVertex3 =
                    signSurfaceBlockMesh.Vertices.Array[signSurfaceBlockMesh.Indices.Array[i * 3 + 2]];
                var p = blockMeshVertex.Position + vector + vector2;
                var p2 = blockMeshVertex2.Position + vector + vector2;
                var p3 = blockMeshVertex3.Position + vector + vector2;
                var textureCoordinates = blockMeshVertex.TextureCoordinates;
                var textureCoordinates2 = blockMeshVertex2.TextureCoordinates;
                var textureCoordinates3 = blockMeshVertex3.TextureCoordinates;
                textureCoordinates.X = MathUtils.Lerp(x, x2, textureCoordinates.X);
                textureCoordinates2.X = MathUtils.Lerp(x, x2, textureCoordinates2.X);
                textureCoordinates3.X = MathUtils.Lerp(x, x2, textureCoordinates3.X);
                textureCoordinates.Y = MathUtils.Lerp(x3, x4, textureCoordinates.Y);
                textureCoordinates2.Y = MathUtils.Lerp(x3, x4, textureCoordinates2.Y);
                textureCoordinates3.Y = MathUtils.Lerp(x3, x4, textureCoordinates3.Y);
                texturedBatch3D.QueueTriangle(p, p2, p3, textureCoordinates, textureCoordinates2,
                    textureCoordinates3, color);
            }
        }

        _primitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
    }

    private class TextData
    {
        public readonly Color[] Colors =
        [
            Color.Black,
            Color.Black,
            Color.Black,
            Color.Black
        ];

        public float Distance;

        public int Light;

        public readonly string[] Lines =
        [
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty
        ];

        public Point3 Point;

        public int? TextureLocation;

        public int ToBeRenderedFrame;

        public string Url = string.Empty;

        public float UsedTextureHeight;

        public float UsedTextureWidth;
    }
}
