namespace Game.Widgets;

/// <summary>
/// Compact read-only player row used by the persistent HUD player list.
/// </summary>
public sealed class PlayerObservationItemWidget : CanvasWidget
{
    private static readonly Subtexture _arrow =
        TextureAtlasManager.GetSubtexture("Textures/Gui/Arrow");

    private static readonly Subtexture _sleep =
        TextureAtlasManager.GetSubtexture("Textures/Atlas/Sleep");

    private readonly LabelWidget _distance = new()
    {
        FontScale = 0.56f,
        Color = new Color(230, 220, 160, 230),
        DropShadow = true,
        VerticalAlignment = WidgetAlignment.Center
    };

    private readonly RectangleWidget _health = new()
    {
        FillColor = new Color(210, 65, 65, 220),
        OutlineThickness = 0f,
        VerticalAlignment = WidgetAlignment.Far
    };

    private readonly RectangleWidget _healthBackground = new()
    {
        FillColor = new Color(0, 0, 0, 120),
        OutlineThickness = 0f,
        VerticalAlignment = WidgetAlignment.Far
    };

    private readonly LabelWidget _name = new()
    {
        FontScale = 0.62f,
        DropShadow = true,
        VerticalAlignment = WidgetAlignment.Center
    };

    private readonly ImageWidget _direction = new()
    {
        Size = new Vector2(10f, 12f),
        HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Center,
        Margin = new Vector2(4f, 0f),
        ColorTransForm = Color.LightGreen
    };

    private readonly ImageWidget _sleeping = new()
    {
        Size = new Vector2(12f),
        HorizontalAlignment = WidgetAlignment.Far,
        VerticalAlignment = WidgetAlignment.Center,
        Margin = new Vector2(5f, 0f)
    };

    private readonly StackPanelWidget _status = new()
    {
        Direction = LayoutDirection.Horizontal,
        HorizontalAlignment = WidgetAlignment.Far,
        VerticalAlignment = WidgetAlignment.Stretch
    };

    private readonly PlayerData _main;

    public Guid PlayerGuid { get; }

    public string PlayerName { get; }

    public PlayerObservationItemWidget(object item, PlayerData main, float width)
    {
        (PlayerGuid, PlayerName) = item switch
        {
            PlayerData player => (player.PlayerGUID, player.Name),
            PlayerListEntry player => (player.PlayerGuid, player.Name),
            _ => throw new InvalidOperationException("Unsupported player observation item.")
        };
        _main = main;
        Size = new Vector2(width, 24f);
        IsHitTestVisible = false;
        _name.Size = new Vector2(MathUtils.Max(width - 90f, 40f), 22f);
        _healthBackground.Size = new Vector2(width, 2f);
        _health.Size = new Vector2(width, 2f);
        _direction.SubTexture = _arrow;
        _sleeping.SubTexture = _sleep;
        _status.Children.Add(_distance);
        _status.Children.Add(_direction);
        _status.Children.Add(_sleeping);
        Children.Add(_healthBackground);
        Children.Add(_health);
        Children.Add(_name);
        Children.Add(_status);
    }

    public override void Update()
    {
        _name.Text = PlayerName;
        var playerData = _main.SubsystemPlayers.FindPlayerData(
            player => player.PlayerGUID == PlayerGuid);
        var playerComponent = playerData?.ComponentPlayer;
        var mainComponent = _main.ComponentPlayer;
        _main.SubsystemPlayers.PlayerList.TryGetValue(PlayerGuid, out var listedPlayer);
        _main.SubsystemPlayers.OnlinePlayerStates.TryGetValue(PlayerGuid, out var playerState);
        var hasPlayerState = _main.SubsystemPlayers.OnlinePlayerStates.ContainsKey(PlayerGuid);
        var isOnline = listedPlayer?.IsOnline ?? playerComponent is not null;
        var isSleeping = playerComponent?.ComponentSleep.IsSleeping ??
                         (hasPlayerState && playerState.IsSleeping);
        var isMainPlayer = PlayerGuid == _main.PlayerGUID;

        _name.Color = !isOnline
            ? new Color(145, 150, 158, 190)
            : isMainPlayer
                ? new Color(255, 205, 135, 235)
                : new Color(150, 220, 170, 230);
        _sleeping.IsVisible = isSleeping;
        _healthBackground.IsVisible = isOnline;
        _health.IsVisible = isOnline;

        if (isOnline)
        {
            var health = playerComponent is not null
                ? MathUtils.Saturate(playerComponent.ComponentHealth.Health)
                : hasPlayerState
                    ? playerState.Health
                    : 1f;
            _health.Size = new Vector2(Size.X * health, 2f);
        }

        var hasPlayerPosition = playerComponent is not null || hasPlayerState;
        var hasMainPosition = mainComponent is not null ||
                              _main.SubsystemPlayers.OnlinePlayerStates.ContainsKey(_main.PlayerGUID);
        var showRelativePosition =
            !isMainPlayer &&
            !isSleeping &&
            isOnline &&
            hasPlayerPosition &&
            hasMainPosition;
        _distance.IsVisible = showRelativePosition;
        _direction.IsVisible = showRelativePosition;
        if (!showRelativePosition)
        {
            return;
        }

        var mainPosition = mainComponent?.ComponentBody.Position ??
                           _main.SubsystemPlayers.OnlinePlayerStates[_main.PlayerGUID].Position;
        var playerPosition = playerComponent?.ComponentBody.Position ?? playerState.Position;
        _distance.Text = $"{Vector2.Distance(mainPosition.XZ, playerPosition.XZ):0}m";
        _direction.RotateAngle = GetAngle(
            _main.GameWidget.ActiveCamera.ViewDirection,
            mainPosition,
            playerPosition);
    }

    private static float GetAngle(Vector3 direction, Vector3 start, Vector3 destination)
    {
        var target = new Vector2(destination.X, destination.Z);
        var origin = new Vector2(start.X, start.Z);
        return Vector2.Angle(new Vector2(direction.X, direction.Z), target - origin);
    }
}
