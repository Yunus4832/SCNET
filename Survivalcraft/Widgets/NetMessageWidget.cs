using Engine.Graphics;
using Engine.Input;

using Game.Commands;
using Game.Modding;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Widgets;

public class NetMessageWidget : CanvasWidget, IDragTargetWidget
{
    private readonly BevelledButtonWidget _addEmojiButton = new()
    {
        CenterColor = Color.SkyBlue,
        BevelColor = Color.White,
        HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Far,
        Text = "更多"
    };

    private readonly BevelledRectangleWidget _bevelled = new()
    {
        CenterColor = new Color(0, 0, 0, 124),
        BevelColor = Color.White,
        VerticalAlignment = WidgetAlignment.Center,
        BevelSize = 2f
    };

    private readonly CanvasWidget _canvasInputArea = new() { VerticalAlignment = WidgetAlignment.Far };

    private readonly CanvasWidget _canvasMsgList = new() { HorizontalAlignment = WidgetAlignment.Center };

    private readonly LabelWidget _hint = new()
    {
        FontScale = 1f,
        VerticalAlignment = WidgetAlignment.Center,
        Text = "输入...",
        Color = new Color(125, 125, 125),
        Margin = new Vector2(5, 0)
    };

    private readonly SubsystemGameInfo _gameInfo;

    private readonly NetPanelWidget _netPanelWidget;

    private readonly SubsystemGameWidgets _subsystemGameWidgets;

    private readonly ResizableListWidget _messageList = new() { Direction = LayoutDirection.Vertical };

    private readonly BevelledButtonWidget _messageTypeButton = new()
    {
        CenterColor = Color.SkyBlue,
        BevelColor = Color.White,
        HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Far,
        Text = "全服"
    };

    private readonly BevelledButtonWidget _positionBtn = new()
    {
        Text = "位置",
        Size = new Vector2(64, 48),
        FontScale = 0.8f
    };

    private readonly BevelledRectangleWidget _rectangleWidget = new()
    {
        CenterColor = new Color(0, 0, 0, 75),
        BevelColor = Color.White,
        BevelSize = 1f
    };

    private readonly BevelledButtonWidget _sendMessageButton = new()
    {
        CenterColor = Color.SkyBlue,
        BevelColor = Color.White,
        HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Far,
        Text = "发送"
    };

    private readonly BevelledButtonWidget _commandButton = new()
    {
        CenterColor = Color.SkyBlue,
        BevelColor = Color.White,
        HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Far,
        Text = "/"
    };

    private readonly CommandSuggestionsWidget _commandSuggestions = new();

    private readonly ListPanelWidget _vLine = new() { Direction = LayoutDirection.Vertical };

    private readonly AutoCanvasWidget _autoWidget = new()
    {
        VerticalAlignment = WidgetAlignment.Center,
        HorizontalAlignment = WidgetAlignment.Near
    };

    public readonly TextBoxWidget EditText = new()
    {
        FontScale = 1f,
        Margin = new Vector2(5, 0),
        HideText = true
    };

    private byte _messageType;

    private readonly CanvasWidget _moreCanvas = new() { IsVisible = false };

    public override WidgetAlignment HorizontalAlignment { get; set; } = WidgetAlignment.Center;

    public override WidgetAlignment VerticalAlignment { get; set; } = WidgetAlignment.Center;

    private PlayerData PlayerData { get; }

    public NetMessageWidget(PlayerData playerData, NetPanelWidget netPanelWidget)
    {
        PlayerData = playerData;
        _gameInfo = PlayerData.Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _netPanelWidget = netPanelWidget;
        Size = new Vector2(660, 240);

        _canvasMsgList.Size = new Vector2(Size.X, 188);
        _canvasMsgList.Margin = new Vector2(5, 0);
        _canvasInputArea.Size = new Vector2(Size.X - 204, 48);
        _bevelled.Size = new Vector2(Size.X, 48);
        _moreCanvas.Size = _canvasMsgList.Size;

        EditText.Size = _bevelled.Size;
        _moreCanvas.Children.Add(_vLine);
        Children.Add(_rectangleWidget);

        _sendMessageButton.Size = new Vector2(60, 60);
        _sendMessageButton.Margin = new Vector2(-6);
        _commandButton.Size = new Vector2(60, 60);
        _commandButton.Margin = new Vector2(-6);
        _addEmojiButton.Size = new Vector2(60, 60);
        _addEmojiButton.Margin = new Vector2(-6);
        _messageTypeButton.Size = new Vector2(60, 60);
        _messageTypeButton.Margin = new Vector2(-6);
        _canvasInputArea.AddChildren(_bevelled);
        _canvasInputArea.AddChildren(_autoWidget);
        _canvasInputArea.AddChildren(EditText);
        _canvasInputArea.AddChildren(_hint);

        var stackP = new StackPanelWidget();
        stackP.Margin = Vector2.Zero;
        stackP.VerticalAlignment = WidgetAlignment.Far;
        stackP.Direction = LayoutDirection.Horizontal;
        stackP.Children.Add(_commandButton);
        stackP.Children.Add(_canvasInputArea);
        stackP.Children.Add(_messageTypeButton);
        stackP.Children.Add(_addEmojiButton);
        stackP.Children.Add(_sendMessageButton);
        Children.Add(_canvasMsgList);
        Children.Add(stackP);
        Children.Add(_moreCanvas);
        _canvasMsgList.Children.Add(_messageList);
        _commandSuggestions.Size = _canvasMsgList.Size;
        _canvasMsgList.Children.Add(_commandSuggestions);
        _messageList.ItemWidgetFactory = delegate(object obj)
        {
            var autoCanvasWidget = new AutoCanvasWidget { Size = new Vector2(float.PositiveInfinity) };
            autoCanvasWidget.ContentText = obj.ToString() ?? string.Empty;
            return autoCanvasWidget;
        };
        _vLine.ItemWidgetFactory =
            obj => obj as Widget ?? throw new InvalidOperationException("input obj is not Widget");
        _commandSuggestions.SuggestionSelected += ApplyCommandSuggestion;
        EditText.TextChanged += widget =>
        {
            _autoWidget.ContentText = widget.Text;
            RefreshCommandSuggestions();
        };
        EditText.CalculateCharacterPosition = (text, position, scale, spacing) =>
        {
            var characterIndex = MathUtils.Clamp(position, 0, text.Length);
            var offset = position > 0 ? 5f : 0;
            var p = EditText.Font.MeasureText(text, 0, characterIndex, new Vector2(scale), spacing).X - offset;
            return p;
        };

        var funcLine = new StackPanelWidget();
        _positionBtn.IsVisible = _gameInfo.WorldSettings.GameMode == GameMode.Creative;
        funcLine.AddChildren(_positionBtn);
        _vLine.AddItem(funcLine);

        for (var i = 1; i < 9; i++)
        {
            var hLine = new StackPanelWidget { Direction = LayoutDirection.Horizontal };
            for (var j = 0; j < 11; j++)
            {
                var id = j + i * 10;
                if (id > 90)
                {
                    break;
                }

                var subtexture = new Subtexture(
                    ContentManager.Get<Texture2D>("Textures/emojis/" + id),
                    Vector2.Zero,
                    Vector2.One
                );
                var bitmap = new BitmapButtonWidget
                {
                    NormalSubtexture = subtexture,
                    ClickedSubtexture = subtexture,
                };
                bitmap.Size = new Vector2(54f);
                bitmap.ClickableWidget.OnClick = () =>
                {
                    EditText.Text += "<em>" + id + "</em>";
                    EditText.HasFocus = false;
                    _moreCanvas.IsVisible = false;
                };
                hLine.Children.Add(bitmap);
            }

            _vLine.AddItem(hLine);
        }

        _subsystemGameWidgets = playerData.SubsystemGameWidgets;
        _subsystemGameWidgets.OnMessageRecieved += AddNetMsg;
    }


    public void DragOver(Widget dragWidget, object data)
    {
    }

    public void DragDrop(Widget dragWidget, object data)
    {
    }

    public void DragOut(Widget dragWidget, object data)
    {
        if (data is not InventoryDragData inventoryDragData)
        {
            return;
        }

        var value = inventoryDragData.Inventory.GetSlotValue(inventoryDragData.SlotIndex);
        var str = $"<b>{value}</b>";
        if (EditText.Text.EndsWith(str))
        {
            EditText.Text = EditText.Text.Substring(0, EditText.Text.Length - str.Length);
        }
    }

    public void DragIn(Widget dragWidget, object data)
    {
        if (data is not InventoryDragData inventoryDragData)
        {
            return;
        }

        var value = inventoryDragData.Inventory.GetSlotValue(inventoryDragData.SlotIndex);
        EditText.Text += $"<b>{value}</b>";
    }

    private void AddNetMsg(string msg)
    {
        _messageList.AddItem(msg);
        if (_messageList.Items.Count > SubsystemGameWidgets.MaxMassageCount)
        {
            _messageList.RemoveItemAt(0);
        }

        _messageList.ScrollToItem(_messageList.Items[^1]);
        PlayerData.ComponentPlayer?.ComponentGui.DisplaySmallMessage(msg, Color.White, true, false);
    }

    public override void Update()
    {
        // Enter发送消息
        if (EditText.HasFocus && Input.IsKeyDownOnce(Key.Enter))
        {
            _sendMessageButton.ClickableWidget.IsClicked = true;
            EditText.HasFocus = true;
        }

        if (_positionBtn.IsClicked)
        {
            if (PlayerData.Project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode == GameMode.Creative)
            {
                _moreCanvas.IsVisible = false;
                if (PlayerData.ComponentPlayer != null)
                {
                    var p = PlayerData.ComponentPlayer.ComponentBody.Position;
                    EditText.Text += $"<p>{p.X:0},{p.Y:0},{p.Z:0}</p>";
                }
                else
                {
                    AddNetMsg("<c=red>当前状态不可发送位置</c>");
                }
            }
        }

        PlayerData.ComponentPlayer?.ComponentInput.AllowHandleInput = !EditText.HasFocus;
        if (_messageTypeButton.IsClicked)
        {
            switch (_messageType)
            {
                case 0:
                    _messageType = 1;
                    _messageTypeButton.Text = "队伍";
                    break;
                case 1:
                    _messageType = 2;
                    _messageTypeButton.Text = "私聊";
                    break;
                case 2:
                    _messageType = 0;
                    _messageTypeButton.Text = "全服";
                    break;
            }
        }

        if (_addEmojiButton.IsClicked)
        {
            _moreCanvas.IsVisible = !_moreCanvas.IsVisible;
        }

        if (_commandButton.IsClicked)
        {
            if (EditText.Text.StartsWith('/'))
            {
                EditText.Text = string.Empty;
                EditText.HasFocus = false;
                _commandSuggestions.Hide();
            }
            else
            {
                EditText.Text = "/";
                SetCommandTextFocus(false);
            }
        }

        if (_sendMessageButton.IsClicked)
        {
            EditText.Text = EditText.Text.Replace("<c=red>", "");
            if (EditText.Text.StartsWith('/'))
            {
                ExecuteCommand(EditText.Text);
                EditText.Text = string.Empty;
            }
            else if (_messageType == 2 && _netPanelWidget.PlayerListWidget.Players.SelectedIndex == null)
            {
                AddNetMsg("<c=red>请先在玩家列表选中要私聊的玩家</c>");
            }
            else if (_messageType == 1 && PlayerData.GroupKey == string.Empty)
            {
                AddNetMsg("<c=red>你还没有创建或加入队伍</c>");
            }
            else
            {
                var vs = new List<byte>();
                switch (_messageType)
                {
                    case 0: break;
                    case 1:
                        if (PlayerData.Project.FindSubsystem<SubsystemPlayers>(true)!.ServerGroups
                            .TryGetValue(PlayerData.GroupKey, out var v))
                        {
                            foreach (var item in v.Members)
                            {
                                var playerData = PlayerData.SubsystemPlayers.FindPlayerData(p => p.PlayerGUID == item);
                                if (playerData is not null)
                                {
                                    vs.Add(playerData.ClientId);
                                }
                            }
                        }

                        break;
                    case 2:
                        if (_netPanelWidget.PlayerListWidget.Players.SelectedIndex != null)
                        {
                            if (_netPanelWidget.PlayerListWidget.Players.Items[
                                    _netPanelWidget.PlayerListWidget.Players.SelectedIndex.Value]
                                is PlayerData data)
                            {
                                vs.Add(data.ClientId);
                            }
                        }

                        break;
                }

                if (!string.IsNullOrEmpty(EditText.Text))
                {
                    if (PlayerData.Project.FindSubsystem<SubsystemPlayers>(true)!.NoMsgPlayerGuidList
                        .Contains(PlayerData.PlayerGUID.ToString()))
                    {
                        DialogsManager.Alert("你已被禁言，不可以发送消息");
                    }
                    else
                    {
                        _subsystemGameWidgets.AddMessage(EditText.Text, PlayerData.Name, _messageType, vs);
                        EditText.Text = string.Empty;
                    }
                }
            }
        }

        _hint.IsVisible = EditText.Text == string.Empty;
    }

    private void ExecuteCommand(string input)
    {
        if (CommonLib.WorkType is WorkType.Client)
        {
            CommonLib.Net.QueuePackage(CommandPackage.CreateRequest(input));
            return;
        }

        var result = CommandExecutor.ExecutePlayer(input, PlayerData);
        var prefix = result.Success ? "<c=green>[指令]</c>" : "<c=red>[指令]</c>";
        AddNetMsg(prefix + result.Message);
    }

    private void RefreshCommandSuggestions()
    {
        var isCommandInput = EditText.Text.StartsWith('/');
        _commandButton.IsChecked = isCommandInput;
        if (!isCommandInput || CurrentModRuntime.Value is not { } runtime)
        {
            _commandSuggestions.Hide();
            return;
        }

        var principal = CommandPrincipal.FromPlayer(PlayerData);
        _commandSuggestions.Refresh(EditText.Text, runtime.Commands, principal);
    }

    private void ApplyCommandSuggestion(CommandSuggestion suggestion)
    {
        if (suggestion.Value.StartsWith('<'))
        {
            SetCommandTextFocus(true);
            return;
        }

        var value = suggestion.IsArgument
            ? CommandLineTokenizer.FormatToken(suggestion.Value)
            : suggestion.Value;
        EditText.Text = CommandLineTokenizer.ReplaceCurrentToken(EditText.Text, value) + " ";
        EditText.CaretPosition = EditText.Text.Length;

        if (CurrentModRuntime.Value is { } runtime)
        {
            var principal = CommandPrincipal.FromPlayer(PlayerData);
            if (runtime.Commands.CanExecute(EditText.Text, principal) &&
                !_commandSuggestions.HasSuggestions)
            {
                var input = EditText.Text;
                EditText.Text = string.Empty;
                EditText.HasFocus = false;
                ExecuteCommand(input);
                return;
            }
        }

        SetCommandTextFocus(false);
    }

    private void SetCommandTextFocus(bool requiresTextInput)
    {
        EditText.HasFocus = requiresTextInput || PlatformManager.Platform is Platform.Desktop;
    }
}
