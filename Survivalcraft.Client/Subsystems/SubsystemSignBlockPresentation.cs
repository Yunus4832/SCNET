using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Blocks.Presentation;
using Game.Network.Transport;

namespace Game.Subsystems;

public sealed class SubsystemSignBlockPresentation : Subsystem, IDrawable, IUpdateable
{
    private const int _surfaceLineMaximumLength = 16;

    public const float MaxVisibilityDistanceSqr = 400f;

    public const float MinUpdateDistance = 2f;

    public const int TextWidth = 128;

    public const int TextHeight = 32;

    public const int MaxTexts = 32;

    private static readonly int[] _drawOrders = [50];

    public bool CopySignsText;

    public bool ShowSignsTexture;

    private bool _disposed;

    private BitmapFont? _font;

    private BitmapFont Font => _font ??= LabelWidget.BitmapFont;

    private readonly List<Vector3> _lastUpdatePositions = [];

    private readonly List<TextData> _nearTexts = [];

    private readonly Dictionary<Point3, TextData> _textsByPoint = [];

    private readonly TextData?[] _textureLocations = new TextData[32];

    private PrimitivesRenderer2D PrimitivesRenderer2D => field ??= new();

    private PrimitivesRenderer3D PrimitivesRenderer3D => field ??= new();

    private RenderTarget2D? _renderTarget;

    private RenderTarget2D RenderTarget => _renderTarget ??= new RenderTarget2D(
        (int)Font.GlyphHeight * 16,
        (int)Font.GlyphHeight * 4 * 32,
        1,
        ColorFormat.Rgba8888,
        DepthFormat.None
    );

    private SubsystemSignBlockBehavior _signs = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemGameWidgets _subsystemViews = null!;

    public int[] DrawOrders => _drawOrders;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _signs = Project.FindSubsystem<SubsystemSignBlockBehavior>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
        _signs.SignDataChanged += OnSignDataChanged;
        _signs.InteractionRequested += OnInteractionRequested;
        foreach (var point in _signs.SignPoints) { OnSignDataChanged(point); }

        Display.DeviceReset += DisplayDeviceReset;
    }

    private void OnSignDataChanged(Point3 point)
    {
        if (_disposed) { return; }

        if (_textsByPoint.Remove(point, out var previous))
        {
            if (previous.TextureLocation is { } slot) { _textureLocations[slot] = null; }

            _nearTexts.Remove(previous);
        }

        if (_signs.GetStoredSignData(point) is { } data)
        {
            var text = new TextData { Point = point, Url = data.Url };
            data.Lines.CopyTo(text.Lines, 0);
            data.Colors.CopyTo(text.Colors, 0);
            _textsByPoint.Add(point, text);
        }

        _lastUpdatePositions.Clear();
    }

    private void OnInteractionRequested(SignInteractionRequest request)
    {
        var character = request.Character;
        if (_disposed ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !character.IsLocallyControlled ||
            !ReferenceEquals(character.Project, Project)) { return; }

        var gui = character.Entity.FindComponent<ComponentGui>(true)!;
        if (request.Kind == SignInteractionKind.View)
        {
            gui.ModalPanelWidget = new SignViewerWidget(
                _signs.GetSignData(request.Point),
                () => gui.ModalPanelWidget = null
            );
        }
        else
        {
            if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Adventure)
            {
                gui.DisplaySmallMessage(
                    Localization.ClientLanguage.Catalog.GetContentWidgets(nameof(SignEditorWidget), "ReadOnly"),
                    Color.White,
                    false,
                    false
                );
                return;
            }

            NetworkSender.SendToServer(
                WorldRuntimeContext.For(Project),
                new OpenSignEditorPackage { Point3 = request.Point }
            );
        }

        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
    }

    public void OpenEditor(ComponentCharacter componentCharacter, Point3 point)
    {
        if (_disposed ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation ||
            !componentCharacter.IsLocallyControlled ||
            !ReferenceEquals(componentCharacter.Project, Project) ||
            _subsystemGameInfo.WorldSettings.GameMode == GameMode.Adventure)
        {
            return;
        }

        componentCharacter.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget = new SignEditorWidget(
            _signs.GetSignData(point),
            signData => SubmitEdit(point, signData),
            () => componentCharacter.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget = null
        );
    }

    internal void SubmitEdit(Point3 point, SignData data)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        NetworkSender.SendToServer(
            WorldRuntimeContext.For(Project),
            new SignBlockPackage(point, data.Lines, data.Colors, data.Url)
        );
    }

    public void Update(float dt)
    {
        if (!_disposed && WorldRuntimeContext.IsProjectActive(Project)) { UpdateRenderTarget(); }
    }

    public void Draw(Camera camera, int drawOrder)
    {
        if (!_disposed && WorldRuntimeContext.IsProjectActive(Project)) { DrawSigns(camera); }
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_signs is not null)
        {
            _signs.SignDataChanged -= OnSignDataChanged;
            _signs.InteractionRequested -= OnInteractionRequested;
        }

        _renderTarget?.Dispose();
        _renderTarget = null;
        Display.DeviceReset -= DisplayDeviceReset;
        _textsByPoint.Clear();
        _nearTexts.Clear();
        _lastUpdatePositions.Clear();
        Array.Clear(_textureLocations);
    }

    private void DisplayDeviceReset()
    {
        if (_disposed) { return; }

        _renderTarget?.Dispose();
        _renderTarget = null;
        InvalidateRenderTarget();
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
            if (string.IsNullOrEmpty(textData.Lines[i]))
            {
                continue;
            }

            var line = textData.Lines[i].Replace("\\", "").ToUpper();
            list.Add(
                line.Length > _surfaceLineMaximumLength
                    ? $"{line[..(_surfaceLineMaximumLength - 3)]}..."
                    : line
            );
            list2.Add(textData.Colors[i]);
        }

        if (list.Count <= 0)
        {
            return;
        }

        var num = list.Max(l => l.Length) * Font.GlyphHeight;
        var num2 = list.Count * Font.GlyphHeight;
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

        for (var j = 0; j < list.Count; j++)
        {
            fontBatch.QueueText(
                position: new Vector2(
                    num4 / 2f,
                    j * Font.GlyphHeight +
                    textData.TextureLocation.Value * (4f * Font.GlyphHeight) +
                    (num5 - num2) / 2f
                ),
                text: list[j],
                depth: 0f,
                color: list2[j],
                anchor: TextAnchor.HorizontalCenter,
                scale: new Vector2(1f / Font.Scale),
                spacing: Vector2.Zero
            );
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
                if (!(Vector3.DistanceSquared(gameWidget.ActiveCamera.ViewPosition, lastUpdatePosition) < 4f))
                {
                    continue;
                }

                flag2 = true;
                break;
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
            var flatBatch = PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None, null, BlendState.Opaque);
            var fontBatch = PrimitivesRenderer2D.FontBatch(
                Font,
                1,
                DepthStencilState.None,
                null,
                BlendState.Opaque,
                SamplerState.PointClamp
            );
            foreach (var textData3 in _textureLocations)
            {
                if (textData3 != null)
                {
                    RenderText(fontBatch, flatBatch, textData3);
                }
            }

            PrimitivesRenderer2D.Flush();
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

        var texturedBatch3D = PrimitivesRenderer3D.TexturedBatch(
            RenderTarget,
            false,
            0,
            DepthStencilState.DepthRead,
            RasterizerState.CullCounterClockwiseScissor,
            null,
            SamplerState.PointClamp
        );
        foreach (var nearText in _nearTexts)
        {
            if (!nearText.TextureLocation.HasValue)
            {
                continue;
            }

            var cellValue =
                _subsystemTerrain.Terrain.GetCellValue(nearText.Point.X, nearText.Point.Y, nearText.Point.Z);
            var num = Terrain.ExtractContents(cellValue);
            if (Modding.Blocks.WorldContentBinding.For(Project).Catalog.Blocks[num] is not SignBlock signBlock)
            {
                continue;
            }

            var data = Terrain.ExtractData(cellValue);
            var signSurfaceBlockMesh =
                ((SignBlockPresentation)Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog.Get(num))
                .GetSignSurfaceBlockMesh(data);
            var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(nearText.Point.X, nearText.Point.Z, false);
            if (chunkAtCell is { HasCurrentLight: true })
            {
                nearText.Light = Terrain.ExtractLight(cellValue);
            }

            var num2 = LightingManager.LightIntensityByLightValue[nearText.Light];
            var color = new Color(num2, num2, num2);
            var x = 0f;
            var x2 = nearText.UsedTextureWidth / (Font.GlyphHeight * 16f);
            var x3 = nearText.TextureLocation.Value / 32f;
            var x4 = (nearText.TextureLocation.Value + nearText.UsedTextureHeight / (Font.GlyphHeight * 4f)) /
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
                texturedBatch3D.QueueTriangle(
                    p,
                    p2,
                    p3,
                    textureCoordinates,
                    textureCoordinates2,
                    textureCoordinates3,
                    color
                );
            }
        }

        PrimitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
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
