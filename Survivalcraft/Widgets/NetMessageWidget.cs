using Engine.Graphics;
using Engine.Input;
using Engine.Serialization;

using Game.Commands;
using Game.Modding;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Widgets;

public class NetMessageWidget : CanvasWidget
{
    private readonly LabelWidget _hint = new()
    {
        FontScale = 1f,
        VerticalAlignment = WidgetAlignment.Center,
        Text = "输入消息或指令...",
        Color = new Color(160, 160, 160),
        Margin = new Vector2(12f, 0f)
    };

    private readonly NetPanelWidget _netPanelWidget;

    private readonly SubsystemGameWidgets _subsystemGameWidgets;

    private readonly ChatTranscriptWidget _transcript = new()
    {
        Size = new Vector2(660f, 220f),
        VerticalAlignment = WidgetAlignment.Near
    };

    private readonly BevelledButtonWidget _messageTypeButton = new()
    {
        CenterColor = Color.SkyBlue,
        BevelColor = Color.White,
        Text = "全服",
        Size = new Vector2(64f, 52f)
    };

    private readonly BevelledButtonWidget _sendMessageButton = new()
    {
        CenterColor = Color.SkyBlue,
        BevelColor = Color.White,
        Text = "发送",
        Size = new Vector2(64f, 52f)
    };

    private readonly BevelledButtonWidget _commandButton = new()
    {
        CenterColor = Color.SkyBlue,
        BevelColor = Color.White,
        Text = "/",
        Size = new Vector2(52f)
    };

    private readonly CommandSuggestionsWidget _commandSuggestions = new()
    {
        Size = new Vector2(660f, 220f)
    };

    public readonly TextBoxWidget EditText = new()
    {
        FontScale = 1f,
        Margin = new Vector2(12f, 0f),
        HideText = false,
        Size = new Vector2(float.PositiveInfinity, 52f)
    };

    private byte _messageType;

    public event Action? CloseRequested;

    public override WidgetAlignment HorizontalAlignment { get; set; } = WidgetAlignment.Center;

    public override WidgetAlignment VerticalAlignment { get; set; } = WidgetAlignment.Far;

    private PlayerData PlayerData { get; }

    public NetMessageWidget(PlayerData playerData, NetPanelWidget netPanelWidget)
    {
        PlayerData = playerData;
        _netPanelWidget = netPanelWidget;
        _subsystemGameWidgets = playerData.SubsystemGameWidgets;

        Size = new Vector2(660f, 280f);
        Margin = new Vector2(16f, 92f);
        ClampToBounds = true;

        Children.Add(_transcript);
        Children.Add(_commandSuggestions);

        var inputRow = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal,
            VerticalAlignment = WidgetAlignment.Far
        };
        inputRow.Children.Add(_commandButton);
        inputRow.Children.Add(CreateInputArea());
        inputRow.Children.Add(_messageTypeButton);
        inputRow.Children.Add(_sendMessageButton);
        Children.Add(inputRow);

        _transcript.ActionRequested += HandleRichTextAction;
        _commandSuggestions.SuggestionSelected += ApplyCommandSuggestion;
        EditText.TextChanged += _ => RefreshCommandSuggestions();
        EditText.Enter += _ => SubmitInput();
        EditText.Escape += _ => CloseRequested?.Invoke();
        EditText.CalculateCharacterPosition = (text, position, scale, spacing) =>
        {
            var characterIndex = MathUtils.Clamp(position, 0, text.Length);
            var offset = position > 0 ? 5f : 0f;
            return EditText.Font.MeasureText(text, 0, characterIndex, new Vector2(scale), spacing).X - offset;
        };

        _subsystemGameWidgets.OnMessageRecieved += AddNetMsg;
    }

    public void FocusInput()
    {
        EditText.HasFocus = true;
        EditText.CaretPosition = EditText.Text.Length;
    }

    public void BeginCommandInput()
    {
        if (!EditText.Text.StartsWith('/'))
        {
            EditText.Text = "/";
        }

        SetCommandTextFocus(false);
    }

    public override void Update()
    {
        PlayerData.ComponentPlayer?.ComponentInput.AllowHandleInput = !EditText.HasFocus;

        if (_messageTypeButton.IsClicked)
        {
            _messageType = (byte)((_messageType + 1) % 3);
            _messageTypeButton.Text = _messageType switch
            {
                1 => "队伍",
                2 => "私聊",
                _ => "全服"
            };
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
                BeginCommandInput();
            }
        }

        if (_sendMessageButton.IsClicked)
        {
            SubmitInput();
        }

        _hint.IsVisible = EditText.Text.Length == 0;
    }

    public override void Dispose()
    {
        _subsystemGameWidgets.OnMessageRecieved -= AddNetMsg;
        base.Dispose();
    }

    private CanvasWidget CreateInputArea()
    {
        var inputArea = new CanvasWidget
        {
            Size = new Vector2(float.PositiveInfinity, 52f),
            ClampToBounds = true
        };
        inputArea.Children.Add(new BevelledRectangleWidget
        {
            CenterColor = new Color(0, 0, 0, 170),
            BevelColor = new Color(255, 255, 255, 110),
            BevelSize = 1f,
            RoundingRadius = 8f
        });
        inputArea.Children.Add(EditText);
        inputArea.Children.Add(_hint);
        return inputArea;
    }

    private void SubmitInput()
    {
        var input = EditText.Text.Trim();
        if (input.Length == 0)
        {
            return;
        }

        if (input.StartsWith('/'))
        {
            EditText.Text = string.Empty;
            ExecuteCommand(input);
            return;
        }

        if (_messageType == 2 && _netPanelWidget.PlayerListWidget.Players.SelectedIndex == null)
        {
            AddNetMsg("<c=red>请先在玩家列表选中要私聊的玩家</c>");
            return;
        }

        if (_messageType == 1 && PlayerData.GroupKey == string.Empty)
        {
            AddNetMsg("<c=red>你还没有创建或加入队伍</c>");
            return;
        }

        var recipients = GetRecipients();
        if (PlayerData.Project.FindSubsystem<SubsystemPlayers>(true)!.NoMsgPlayerGuidList
            .Contains(PlayerData.PlayerGUID.ToString()))
        {
            DialogsManager.Alert("你已被禁言，不可以发送消息");
            return;
        }

        _subsystemGameWidgets.AddMessage(input, PlayerData.Name, _messageType, recipients);
        EditText.Text = string.Empty;
    }

    private List<byte> GetRecipients()
    {
        var recipients = new List<byte>();
        if (_messageType == 1 &&
            PlayerData.Project.FindSubsystem<SubsystemPlayers>(true)!.ServerGroups
                .TryGetValue(PlayerData.GroupKey, out var group))
        {
            foreach (var member in group.Members)
            {
                var player = PlayerData.SubsystemPlayers.FindPlayerData(data => data.PlayerGUID == member);
                if (player is not null)
                {
                    recipients.Add(player.ClientId);
                }
            }
        }
        else if (_messageType == 2 &&
                 _netPanelWidget.PlayerListWidget.Players.SelectedItem is PlayerData player)
        {
            recipients.Add(player.ClientId);
        }

        return recipients;
    }

    private void AddNetMsg(string message)
    {
        _transcript.MaximumMessages = SubsystemGameWidgets.MaxMassageCount;
        _transcript.AddMessage(message);
        PlayerData.ComponentPlayer?.ComponentGui.DisplaySmallMessage(message, Color.White, true, false);
    }

    private void HandleRichTextAction(RichTextAction action)
    {
        if (action.Kind != RichTextActionKind.Position ||
            !HumanReadableConverter.TryConvertFromString<Vector3>(action.Value, out var position) ||
            CommonLib.MainPlayer is null)
        {
            return;
        }

        CommonLib.MainPlayer.ComponentBody.Position = position;
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
