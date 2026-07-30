using System.Xml.Linq;

using Game.Commands;
using Game.Messaging;

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

    private readonly MessageHistoryOverlayWidget? _historyOverlayWidget;

    private readonly GameMessageService _messageService;

    private readonly bool _messagingEnabled;

    private readonly ChatTranscriptWidget _transcript = new()
    {
        Size = new Vector2(590f, 298f),
        MaximumMessages = GameMessageService.MaximumHistoryCount,
        Padding = 10f,
        IsBackgroundVisible = false,
        VerticalAlignment = WidgetAlignment.Near
    };

    private readonly BevelledButtonWidget _channelButton =
        MultiplayerUiStyle.CreateButton(
            MultiplayerUiStyle.Text("Global"),
            new Vector2(88f, 54f));

    private readonly BevelledButtonWidget _sendMessageButton =
        MultiplayerUiStyle.CreateButton(
            MultiplayerUiStyle.Text("Send"),
            new Vector2(88f, 54f));

    private readonly BevelledButtonWidget _commandButton =
        MultiplayerUiStyle.CreateButton("/", new Vector2(54f));

    private readonly CommandSuggestionsWidget _commandSuggestions = new()
    {
        Size = new Vector2(590f, 298f)
    };

    private readonly BevelledButtonWidget _historyOverlayButton =
        MultiplayerUiStyle.CreateButton(string.Empty, new Vector2(116f, 54f));

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

    public bool IsCommandInput => EditText.Text.StartsWith('/');

    public MessagePanelWidget(
        PlayerData playerData,
        MessageHistoryOverlayWidget? historyOverlayWidget,
        bool messagingEnabled = true)
    {
        PlayerData = playerData;
        _historyOverlayWidget = historyOverlayWidget;
        _messageService = playerData.SubsystemGameWidgets.Messages;
        _messagingEnabled = messagingEnabled;

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
        if (_messagingEnabled)
        {
            inputRow.Children.Add(_channelButton);
            inputRow.Children.Add(_historyOverlayButton);
        }

        inputRow.Children.Add(_sendMessageButton);
        inputHost.Children.Add(inputRow);

        _commandSuggestions.SuggestionSelected += ApplyCommandSuggestion;
        EditText.TextChanged += _ => RefreshCommandSuggestions();
        EditText.Enter += _ => SubmitOrClose();
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
        if (_historyOverlayWidget != null)
        {
            UpdateHistoryOverlayButton();
        }
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

        RefreshCommandSuggestions();
        SetCommandTextFocus(false);
    }

    public void CancelCommandInput()
    {
        ResetInput();
        if (!_messagingEnabled &&
            PlayerData.ComponentPlayer?.ComponentGui.ModalPanelWidget == this)
        {
            PlayerData.ComponentPlayer.ComponentGui.ModalPanelWidget = null;
        }
    }

    public void ResetInput()
    {
        EditText.Text = string.Empty;
        EditText.HasFocus = false;
        _commandSuggestions.Hide();
    }

    public override void Update()
    {
        if (_messagingEnabled && _channelButton.IsClicked)
        {
            _messageChannel = (GameMessageChannel)(((int)_messageChannel + 1) % 2);
            MultiplayerUiStyle.SetButtonText(
                _channelButton,
                _messageChannel switch
                {
                    GameMessageChannel.Team => MultiplayerUiStyle.Text("Team"),
                    _ => MultiplayerUiStyle.Text("Global")
                });
        }

        if (_commandButton.IsClicked)
        {
            if (IsCommandInput)
            {
                CancelCommandInput();
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

        if (_historyOverlayWidget != null && _historyOverlayButton.IsClicked)
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

        if (!_messagingEnabled)
        {
            BeginCommandInput();
            return;
        }

        CommandGateway.Submit(
            PlayerData,
            new SendChatMessageCommand(_messageChannel, input));
        EditText.Text = string.Empty;
    }

    private void SubmitOrClose()
    {
        if (EditText.Text.Trim() == "/")
        {
            CancelCommandInput();
            return;
        }

        if (!string.IsNullOrWhiteSpace(EditText.Text))
        {
            SubmitInput();
            return;
        }

        EditText.Text = string.Empty;
        EditText.HasFocus = false;
        _commandSuggestions.Hide();
        if (PlayerData.ComponentPlayer?.ComponentGui.ModalPanelWidget == this)
        {
            PlayerData.ComponentPlayer.ComponentGui.ModalPanelWidget = null;
        }
    }

    private void AddNetMsg(GameMessage message)
    {
        _transcript.AddMessage(message);
    }

    private void ExecuteCommand(string input)
    {
        CommandGateway.Submit(PlayerData, input);
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
        var textAdapter = new TextCommandAdapter(runtime.Commands);
        _commandSuggestions.SetSuggestions(
            textAdapter.Suggest(EditText.Text, principal)
                .Concat(textAdapter.Suggest(
                    EditText.Text,
                    CommandPrincipal.ApplicationUser,
                    CommandInvocationChannel.Text))
                .GroupBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => item.Value, StringComparer.OrdinalIgnoreCase));
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
            var adapter = new TextCommandAdapter(runtime.Commands);
            if ((adapter.CanExecute(EditText.Text, principal) ||
                 adapter.CanExecute(
                     EditText.Text,
                     CommandPrincipal.ApplicationUser,
                     CommandInvocationChannel.Text)) &&
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
        if (_historyOverlayWidget == null)
        {
            return;
        }

        MultiplayerUiStyle.SetButtonText(
            _historyOverlayButton,
            _historyOverlayWidget.DisplayEnabled
                ? MultiplayerUiStyle.Text("OverlayOn")
                : MultiplayerUiStyle.Text("OverlayOff"));
        _historyOverlayButton.IsChecked = _historyOverlayWidget.DisplayEnabled;
    }

    private void DisplayInputError(string message)
    {
        _messageService.DisplayLocal(
            GameMessage.System(message, GameMessageTone.Error));
    }
}
