namespace Game.Widgets;

/// <summary>
///     Compact read-only player row used by the persistent HUD player list.
/// </summary>
public sealed class CharacterObservationItemWidget : CanvasWidget
{
    private static readonly Subtexture _arrow =
        TextureAtlasManager.GetSubtexture("Textures/Atlas/Arrow");

    private static readonly Subtexture _sleep =
        TextureAtlasManager.GetSubtexture("Textures/Atlas/Sleep");

    private readonly LabelWidget _distance = new()
    {
        FontScale = 0.49f,
        Color = new Color(230, 220, 160, 230),
        DropShadow = true,
        VerticalAlignment = WidgetAlignment.Center
    };

    private readonly BevelledRectangleWidget _health = new()
    {
        CenterColor = new Color(92, 205, 112, 220),
        BevelColor = Color.Transparent,
        BevelSize = 0f,
        RoundingRadius = 1f,
        ShadowColor = Color.Transparent,
        ShadowSize = 0f,
        VerticalAlignment = WidgetAlignment.Far,
        Margin = new Vector2(0f, 1f)
    };

    private readonly BevelledRectangleWidget _healthBackground = new()
    {
        CenterColor = new Color(0, 0, 0, 145),
        BevelColor = Color.Transparent,
        BevelSize = 0f,
        RoundingRadius = 1f,
        ShadowColor = Color.Transparent,
        ShadowSize = 0f,
        VerticalAlignment = WidgetAlignment.Far,
        Margin = new Vector2(0f, 1f)
    };

    private readonly LabelWidget _name = new()
    {
        FontScale = 0.55f,
        DropShadow = true,
        VerticalAlignment = WidgetAlignment.Center,
        ClampToBounds = true,
        Ellipsis = true,
        MaxLines = 1,
        Margin = new Vector2(3f, 0f)
    };

    private readonly ImageWidget _direction = new()
    {
        Size = new Vector2(9f, 10f),
        HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Center,
        Margin = new Vector2(4f, 0f),
        ColorTransForm = Color.LightGreen
    };

    private readonly ImageWidget _sleeping = new()
    {
        Size = new Vector2(10f),
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

    private readonly CanvasWidget _statusHost = new()
    {
        Size = new Vector2(64f, 19f),
        HorizontalAlignment = WidgetAlignment.Far,
        VerticalAlignment = WidgetAlignment.Near,
        IsHitTestVisible = false
    };

    private readonly CharacterData _main;

    public Guid CharacterId { get; }

    public string CharacterName { get; }

    public CharacterObservationItemWidget(object item, CharacterData main, float width)
    {
        (CharacterId, CharacterName) = item switch
        {
            CharacterData player => (player.CharacterId, player.Name),
            CharacterListEntry player => (player.CharacterId, player.Name),
            _ => throw new InvalidOperationException("Unsupported player observation item.")
        };
        _main = main;
        Size = new Vector2(width, 21f);
        IsHitTestVisible = false;
        _name.Size = new Vector2(width, 19f);
        _healthBackground.Size = new Vector2(width, 2f);
        _health.Size = new Vector2(width, 2f);
        _direction.SubTexture = _arrow;
        _sleeping.SubTexture = _sleep;
        _status.Children.Add(_distance);
        _status.Children.Add(_direction);
        _status.Children.Add(_sleeping);
        _statusHost.Children.Add(_status);
        Children.Add(_healthBackground);
        Children.Add(_health);
        Children.Add(_name);
        Children.Add(_statusHost);
    }

    public override void Update()
    {
        _name.Text = CharacterName;
        var characterData = _main.SubsystemCharacters.FindCharacterData(player => player.CharacterId == CharacterId);
        var playerComponent = characterData?.ComponentCharacter;
        var mainComponent = _main.ComponentCharacter;
        _main.SubsystemCharacters.CharacterList.TryGetValue(CharacterId, out var listedPlayer);
        _main.SubsystemCharacters.OnlineCharacterStates.TryGetValue(CharacterId, out var playerState);
        var hasPlayerState = _main.SubsystemCharacters.OnlineCharacterStates.ContainsKey(CharacterId);
        var isOnline = listedPlayer?.IsOnline ?? playerComponent is not null;
        var isSleeping = playerComponent?.ComponentSleep.IsSleeping ??
                         (hasPlayerState && playerState.IsSleeping);
        var isLocalCharacter = CharacterId == _main.CharacterId;

        _name.Color = !isOnline
            ? new Color(145, 150, 158, 190)
            : isLocalCharacter
                ? new Color(255, 205, 135, 235)
                : new Color(150, 220, 170, 230);
        _sleeping.IsVisible = isSleeping;
        var showHealth = isOnline &&
                         _main.SubsystemGameInfo.WorldSettings.GameMode != GameMode.Creative;
        _healthBackground.IsVisible = showHealth;
        _health.IsVisible = showHealth;

        if (showHealth)
        {
            var health = playerComponent is not null
                ? MathUtils.Saturate(playerComponent.ComponentHealth.Health)
                : hasPlayerState
                    ? playerState.Health
                    : 1f;
            _health.Size = new Vector2(Size.X * health, 2f);
            _health.CenterColor = health > 0.6f
                ? new Color(92, 205, 112, 220)
                : health > 0.3f
                    ? new Color(230, 190, 72, 220)
                    : new Color(218, 76, 70, 225);
        }

        var hasPlayerPosition = playerComponent is not null || hasPlayerState;
        var hasMainPosition = mainComponent is not null ||
                              _main.SubsystemCharacters.OnlineCharacterStates.ContainsKey(_main.CharacterId);
        var showRelativePosition =
            !isLocalCharacter &&
            !isSleeping &&
            isOnline &&
            hasPlayerPosition &&
            hasMainPosition;
        _distance.IsVisible = showRelativePosition;
        _direction.IsVisible = showRelativePosition;
        _statusHost.IsVisible = isSleeping || showRelativePosition;
        _name.Size = new Vector2(
            _statusHost.IsVisible ? MathUtils.Max(Size.X - _statusHost.Size.X, 40f) : Size.X,
            19f
        );
        if (!showRelativePosition)
        {
            return;
        }

        var mainPosition = mainComponent?.ComponentBody.Position ??
                           _main.SubsystemCharacters.OnlineCharacterStates[_main.CharacterId].Position;
        var playerPosition = playerComponent?.ComponentBody.Position ?? playerState.Position;
        _distance.Text = $"{Vector2.Distance(mainPosition.XZ, playerPosition.XZ):0}m";
        _direction.RotateAngle = GetAngle(
            _main.GameWidget.ActiveCamera.ViewDirection,
            mainPosition,
            playerPosition
        );
    }

    private static float GetAngle(Vector3 direction, Vector3 start, Vector3 destination)
    {
        var target = new Vector2(destination.X, destination.Z);
        var origin = new Vector2(start.X, start.Z);
        return Vector2.Angle(new Vector2(direction.X, direction.Z), target - origin);
    }
}
