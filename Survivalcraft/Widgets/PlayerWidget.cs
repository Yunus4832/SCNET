using System.Xml.Linq;
using Game.NetWork;

namespace Game.Widgets;

public class PlayerWidget : CanvasWidget
{
    private const string _typeName = "PlayerWidget";

    private readonly LabelWidget _detailsLabel;

    private readonly ButtonWidget _editButton;

    private readonly LabelWidget _nameLabel;

    private readonly PlayerData _playerData;

    private readonly PlayerModelWidget _playerModel;

    public PlayerWidget(PlayerData playerData, CharacterSkinsCache characterSkinsCache)
    {
        var node = ContentManager.Get<XElement>("Widgets/PlayerWidget");
        LoadContents(this, node);
        _playerModel = Children.Find<PlayerModelWidget>("PlayerModel")!;
        _nameLabel = Children.Find<LabelWidget>("Name")!;
        _detailsLabel = Children.Find<LabelWidget>("Details")!;
        _editButton = Children.Find<ButtonWidget>("EditButton")!;
        _playerModel.CharacterSkinsCache = characterSkinsCache;
        _playerData = playerData;
        if (CommonLib.WorkType == WorkType.Client)
        {
            _editButton.IsEnabled = _playerData.IsMainPlayer;
        }
    }

    public override void Update()
    {
        var subsystemGameInfo = _playerData.SubsystemPlayers.Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _playerModel.PlayerClass = _playerData.PlayerClass;
        _playerModel.CharacterSkinName = _playerData.CharacterSkinName;
        _nameLabel.Text = _playerData.Name;
        _detailsLabel.Text =
            $"{_playerData.Name}已在游戏生存{(subsystemGameInfo.TotalElapsedGameTime - _playerData.LastSpawnTime) / 1200.0:N1}天";
        if (_playerData.IsMainPlayer)
        {
            _nameLabel.Color = Color.Green;
            _detailsLabel.Color = Color.Green;
        }

        if (!_editButton.IsClicked)
        {
            return;
        }

        if (CommonLib.WorkType == WorkType.Local && !_playerData.IsMainPlayer)
        {
            DialogsManager.ShowDialog(
                this,
                new MessageDialog(
                    "提示",
                    "只能对自己进行操作",
                    "确定",
                    "取消",
                    _ => { DialogsManager.HideAllDialogs(); }
                )
            );
        }
        else
        {
            ScreensManager.SwitchScreen("Player", PlayerScreen.Mode.Edit, _playerData);
        }
    }
}
