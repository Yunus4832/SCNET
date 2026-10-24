using Game.Network;

namespace Game.Widgets;

/// <summary>
///     玩家列表组件
/// </summary>
public class CharacterListWidget : CanvasWidget
{
    public enum ListKind
    {
        Characters = 0,

        SameTeamCharacters = 1
    }

    /// <summary>
    ///     主玩家
    /// </summary>
    private readonly CharacterData _localCharacter;

    /// <summary>
    ///     展示类型
    /// </summary>
    private ListKind _kind;

    public ListKind Kind
    {
        get => _kind;
        set
        {
            if (_kind == value)
            {
                return;
            }

            _kind = value;
            RefreshList();
        }
    }

    /// <summary>
    ///     玩家列表
    /// </summary>
    public readonly ListPanelWidget Characters = new() { Direction = LayoutDirection.Vertical };

    public override WidgetAlignment HorizontalAlignment { get; set; } = WidgetAlignment.Center;

    public override WidgetAlignment VerticalAlignment { get; set; } = WidgetAlignment.Center;

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="main">主玩家</param>
    /// <param name="kind">列表数据范围</param>
    /// <param name="isReadOnly"></param>
    /// <param name="size"></param>
    public CharacterListWidget(
        CharacterData main,
        ListKind kind,
        bool isReadOnly = false,
        Vector2? size = null
    )
    {
        Size = size ?? new Vector2(220f, 120f);

        _kind = kind;
        var isReadOnly1 = isReadOnly;

        var itemWidth = MathUtils.Max(Size.X - (isReadOnly ? 8f : 32f), 0f);
        Characters.HorizontalAlignment = WidgetAlignment.Center;
        Children.Add(Characters);

        _localCharacter = main;
        Characters.ItemSize = isReadOnly1 ? 21f : 52f;
        Characters.SelectionColor = MultiplayerUiStyle.ListSelectionColor;
        Characters.IsHitTestVisible = !isReadOnly1;
        Characters.IsSelectionEnabled = !isReadOnly1;
        if (isReadOnly1)
        {
            Characters.ScrollPosition = 0f;
            Characters.ScrollSpeed = 0f;
        }

        Characters.ItemWidgetFactory = obj =>
        {
            if (!isReadOnly1)
            {
                return new CharacterManagementItemWidget(obj, _localCharacter, _kind, itemWidth);
            }

            return new CharacterObservationItemWidget(obj, _localCharacter, itemWidth);
        };

        _localCharacter.SubsystemCharacters.CharacterAdded += CharacterDataChange;
        _localCharacter.SubsystemCharacters.CharacterRemoved += CharacterDataChange;
        _localCharacter.SubsystemCharacters.CharacterListChanged += RefreshList;
        _localCharacter.EntityRemoved += Unsubscribe;
        RefreshList();
    }

    public override void Dispose()
    {
        Unsubscribe();
        base.Dispose();
    }

    private void Unsubscribe()
    {
        _localCharacter.SubsystemCharacters.CharacterAdded -= CharacterDataChange;
        _localCharacter.SubsystemCharacters.CharacterRemoved -= CharacterDataChange;
        _localCharacter.SubsystemCharacters.CharacterListChanged -= RefreshList;
        _localCharacter.EntityRemoved -= Unsubscribe;
    }

    private void CharacterDataChange(CharacterData player)
    {
        RefreshList();
    }

    public void RefreshList()
    {
        switch (_kind)
        {
            case ListKind.Characters:
                PopulateAllPlayers();
                break;
            case ListKind.SameTeamCharacters:
                PopulateTeamPlayers();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void PopulateTeamPlayers()
    {
        Characters.ClearItems();
        if (PlayerSessionQuery.GetController(_localCharacter.CharacterId) is not { } subject)
        {
            return;
        }

        var controlled = CharacterTeamQuery.GetControlledTeamCharacters(_localCharacter.Project, subject)
            .Select(character => character.CharacterId).ToHashSet();
        foreach (var item in GetPlayerListItems())
        {
            if (controlled.Contains(item.Entry.CharacterId)) { Characters.AddItem(item.Item); }
        }
    }

    private void PopulateAllPlayers()
    {
        Characters.ClearItems();
        foreach (var item in GetPlayerListItems())
        {
            Characters.AddItem(item.Item);
        }
    }

    private IEnumerable<(CharacterListEntry Entry, object Item)> GetPlayerListItems()
    {
        return from entry in _localCharacter.SubsystemCharacters.CharacterList.Values
                .OrderByDescending(player => player.IsOnline)
                .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            let activePlayer = _localCharacter.SubsystemCharacters.Characters
                .Find(player => player.CharacterId == entry.CharacterId)
            select ((CharacterListEntry Entry, object Item))(entry, activePlayer is not null ? activePlayer : entry);
    }
}
