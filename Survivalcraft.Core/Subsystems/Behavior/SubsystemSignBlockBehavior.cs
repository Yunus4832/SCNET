using System.Globalization;

using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemSignBlockBehavior : SubsystemBlockBehavior
{
    private readonly Dictionary<Point3, SignData> _signs = [];

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemNetworkInterest? _networkInterest;

    public event Action<Point3>? SignDataChanged;

    public event Action<SignInteractionRequest>? InteractionRequested;

    public IReadOnlyCollection<Point3> SignPoints => _signs.Keys;

    public override ResourceId[] HandledBlocks =>
    [
        ResourceId.Parse("game:stick"), ResourceId.Parse("game:wooden_posted_sign"),
        ResourceId.Parse("game:wooden_attached_sign"), ResourceId.Parse("game:iron_posted_sign"),
        ResourceId.Parse("game:iron_attached_sign")
    ];

    public SignData? GetStoredSignData(Point3 point)
    {
        if (!_signs.TryGetValue(point, out var value))
        {
            return null;
        }

        var signData = new SignData
        {
            Lines = value.Lines.ToArray(),
            Colors = value.Colors.ToArray(),
            Url = value.Url
        };
        return signData;
    }

    public SignData? GetSignData(Point3 point)
    {
        var signData = GetStoredSignData(point);
        if (signData is null) { return null; }

        var blockedKeywords = _subsystemGameInfo.WorldSettings.KeywordBlocking
            .Split([';'], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < signData.Lines.Length; i++)
        {
            foreach (var keyword in blockedKeywords)
            {
                signData.Lines[i] = signData.Lines[i].Replace(keyword, "*");
            }
        }

        return signData;
    }

    public void SetSignData(Point3 point, string[] lines, Color[] colors, string url)
    {
        if (lines.Length != 4 || colors.Length != 4)
        {
            throw new ArgumentException("Signs require four lines and colors.");
        }

        _signs[point] = new SignData { Lines = lines.ToArray(), Colors = colors.ToArray(), Url = url };
        SignDataChanged?.Invoke(point);
    }

    private void SendChunkState(WorldConnection client, Point2 chunk)
    {
        foreach (var (point, data) in _signs.Where(pair => pair.Key.X >> 4 == chunk.X && pair.Key.Z >> 4 == chunk.Y))
        {
            NetworkSender.SendTo(client, new SignBlockPackage(point, data.Lines, data.Colors, data.Url));
        }
    }

    public void ClearChunkState(Point2 chunk)
    {
        foreach (var point in _signs.Keys.Where(point => point.X >> 4 == chunk.X && point.Z >> 4 == chunk.Y).ToArray())
        {
            _signs.Remove(point);
            SignDataChanged?.Invoke(point);
        }
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellValueFast = SubsystemTerrain.Terrain.GetCellValueFast(x, y, z);
        var num = Terrain.ExtractContents(cellValueFast);
        var data = Terrain.ExtractData(cellValueFast);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[num];
        switch (block)
        {
            case AttachedSignBlock:
            {
                var point = CellFace.FaceToPoint3(AttachedSignBlock.GetFace(data));
                var x2 = x - point.X;
                var y2 = y - point.Y;
                var z2 = z - point.Z;
                var cellValue = SubsystemTerrain.Terrain.GetCellValue(x2, y2, z2);
                var cellContents = Terrain.ExtractContents(cellValue);
                if (!WorldContentBinding.For(Project).Catalog.Blocks[cellContents]
                        .IsCollidable(cellValue))
                {
                    SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                }

                break;
            }
            case PostedSignBlock:
            {
                var num2 = PostedSignBlock.GetHanging(data)
                    ? SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z)
                    : SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z);

                if (!WorldContentBinding.For(Project).Catalog.Blocks[Terrain.ExtractContents(num2)]
                        .IsCollidable(num2))
                {
                    SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                }

                break;
            }
        }
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        if (componentMiner.ComponentCharacter is { IsLocallyControlled: true } character)
        {
            InteractionRequested?.Invoke(
                new SignInteractionRequest(character, raycastResult.CellFace.Point, SignInteractionKind.View)
            );
        }

        return true;
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentCharacter character)
    {
        if (character.IsLocallyControlled)
        {
            InteractionRequested?.Invoke(
                new SignInteractionRequest(character, new Point3(x, y, z), SignInteractionKind.Edit)
            );
        }

        return true;
    }

    public bool CanEditSign(Point3 point, WorldConnection? sender)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority ||
            sender is null ||
            _subsystemGameInfo.WorldSettings.GameMode == GameMode.Adventure)
        {
            return false;
        }

        if (!Project.FindSubsystem<SubsystemNetworkInterest>(true)!
                .IsPositionRelevant(sender, new Vector2(point.X, point.Z)))
        {
            return false;
        }

        var contents = _subsystemTerrain.Terrain.GetCellContents(point.X, point.Y, point.Z);
        return WorldContentBinding.For(Project).Catalog.Blocks[contents] is SignBlock;
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        var point = new Point3(x, y, z);
        if (_signs.Remove(point)) { SignDataChanged?.Invoke(point); }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            _networkInterest = Project.FindSubsystem<SubsystemNetworkInterest>(true)!;
            _networkInterest.ChunkEntered += SendChunkState;
        }

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
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var num = 0;
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Texts", valuesDictionary2);
        if (Project.SendToClientMode)
        {
            return;
        }

        foreach (var (point, value) in _signs)
        {
            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary3.SetValue("Point", point);
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
        if (_networkInterest is not null) { _networkInterest.ChunkEntered -= SendChunkState; }

        SignDataChanged = null;
        InteractionRequested = null;
        _signs.Clear();
    }
}
