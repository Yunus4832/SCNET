using Engine.Graphics;
using Engine.Input;
using System.Xml.Linq;

using Game.Commands;
using Game.Messaging;
using Game.Modding;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Widgets;

public sealed class MessagePanelWidget : CanvasWidget
{
    private readonly LabelWidget _hint = new()
    {
        FontScale = 1f,
        VerticalAlignment = WidgetAlignment.Center,
        Text = MultiplayerUiStyle.Text("MessageInputHint"),
        Color = new Color(160, 160, 160),
        Margin = new Vector2(12f, 0f)
    };

    private readonly MessageHistoryOverlayWidget _historyOverlayWidget;

    private readonly GameMessageService _messageService;

    private readonly ChatTranscriptWidget _transcript = new()
    {
        Size = new Vector2(590f, 298f),
        MaximumMessages = GameMessageService.MaximumHistoryCount,
        Padding = 10f,
        IsBackgroundVisible = false,
        VerticalAlignment = WidgetAlignment.Near
    };

    private readonly BevelledButtonWidget _channelButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("Global"), new Vector2(74f, 54f));

    private readonly BevelledButtonWidget _sendMessageButton =
        MultiplayerUiStyle.CreateButton(MultiplayerUiStyle.Text("Send"), new Vector2(74f, 54f));

    private readonly BevelledButtonWidget _commandButton =
        MultiplayerUiStyle.CreateButton("/", new Vector2(54f));

    private readonly CommandSuggestionsWidget _commandSuggestions = new()
    {
        Size = new Vector2(590f, 298f)
    };

    private readonly BevelledButtonWidget _historyOverlayButton =
        MultiplayerUiStyle.CreateButton(string.Empty, new Vector2(104f, 54f));

    public readonly TextBoxWidget EditText = new()
    {
        FontScale = 1f,
        Margin = new Vector2(12f, 0f),
        HideText = false,
        Size = new Vector2(float.PositiveInfinity, 54f)
    };

    private GameMessageChannel _messageChannel;

    public override WidgetAlignment HorizontalAlignment { get; set; } = WidgetAlignment.Center;

    public override WidgetAlignment VerticalAlignment { get; set; } = WidgetAlignment.Center;

    private PlayerData PlayerData { get; }

    public MessagePanelWidget(
        PlayerData playerData,
        MessageHistoryOverlayWidget historyOverlayWidget)
    {
        PlayerData = playerData;
        _historyOverlayWidget = historyOverlayWidget;
        _messageService = playerData.SubsystemGameWidgets.Messages;

        LoadContents(this, ContentManager.Get<XElement>("Widgets/MessagePanelWidget"));
        var transcriptHost = Children.Find<CanvasWidget>("TranscriptHost")!;
        var inputHost = Children.Find<CanvasWidget>("InputHost")!;

        transcriptHost.Children.Add(_transcript);
        transcriptHost.Children.Add(_commandSuggestions);

        var inputRow = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal,
            VerticalAlignment = WidgetAlignment.Far
        };
        inputRow.Children.Add(_commandButton);
        inputRow.Children.Add(CreateInputArea());
        inputRow.Children.Add(_channelButton);
        inputRow.Children.Add(_historyOverlayButton);
        inputRow.Children.Add(_sendMessageButton);
        inputHost.Children.Add(inputRow);

        _commandSuggestions.SuggestionSelected += ApplyCommandSuggestion;
        EditText.TextChanged += _ => RefreshCommandSuggestions();
        EditText.Enter += _ => SubmitInput();
        EditText.CalculateCharacterPosition = (text, position, scale, spacing) =>
        {
            var characterIndex = MathUtils.Clamp(position, 0, text.Length);
            var offset = position > 0 ? 5f : 0f;
            return EditText.Font.MeasureText(text, 0, characterIndex, new Vector2(scale), spacing).X - offset;
        };

        foreach (var message in _messageService.History)
        {
            _transcript.AddMessage(message);
        }

        _messageService.MessageReceived += AddNetMsg;
        UpdateHistoryOverlayButton();
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
        if (_channelButton.IsClicked)
        {
            _messageChannel = (GameMessageChannel)(((int)_messageChannel + 1) % 2);
            _channelButton.Text = _messageChannel switch
            {
                GameMessageChannel.Team => MultiplayerUiStyle.Text("Team"),
                _ => MultiplayerUiStyle.Text("Global")
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

        if (_historyOverlayButton.IsClicked)
        {
            _historyOverlayWidget.DisplayEnabled = !_historyOverlayWidget.DisplayEnabled;
            SettingsManager.Current.ShowMessageHistoryOverlay = _historyOverlayWidget.DisplayEnabled;
            UpdateHistoryOverlayButton();
        }

        _hint.IsVisible = EditText.Text.Length == 0;
    }

    public override void Dispose()
    {
        _messageService.MessageReceived -= AddNetMsg;
        base.Dispose();
    }

    private CanvasWidget CreateInputArea()
    {
        var inputArea = new CanvasWidget
        {
            Size = new Vector2(float.PositiveInfinity, 54f),
            ClampToBounds = true
        };
        inputArea.Children.Add(MultiplayerUiStyle.CreateInsetArea());
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

        if (_messageChannel is GameMessageChannel.Team && PlayerData.GroupKey == string.Empty)
        {
            DisplayInputError("你还没有创建或加入队伍");
            return;
        }

        _messageService.Publish(GameMessage.Chat(_messageChannel, PlayerData.Name, input));
        EditText.Text = string.Empty;
    }

    private void AddNetMsg(GameMessage message)
    {
        _transcript.AddMessage(message);
    }

    private void ExecuteCommand(string input)
    {
        if (CommonLib.WorkType is WorkType.Client)
        {
            CommonLib.Net.QueuePackage(CommandPackage.CreateRequest(input));
            return;
        }

        var result = CommandExecutor.ExecutePlayer(input, PlayerData);
        CommandResultPublisher.Publish(PlayerData.Project, result);
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

    private void UpdateHistoryOverlayButton()
    {
        _historyOverlayButton.Text = _historyOverlayWidget.DisplayEnabled
            ? MultiplayerUiStyle.Text("OverlayOn")
            : MultiplayerUiStyle.Text("OverlayOff");
        _historyOverlayButton.IsChecked = _historyOverlayWidget.DisplayEnabled;
    }

    private void DisplayInputError(string message)
    {
        _messageService.DisplayLocal(
            GameMessage.System(message, GameMessageTone.Error));
    }
}
