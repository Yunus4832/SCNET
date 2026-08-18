using System.Text;

using Android.Content.PM;
using Android.Graphics;
using Android.Text.Method;
using Android.Views;
using Android.Views.InputMethods;

using Game;
using Game.Commands;
using Game.Localization;

using AndroidAlertDialog = Android.App.AlertDialog;
using AndroidProviderSettings = Android.Provider.Settings;
using Color = Android.Graphics.Color;

namespace Survivalcraft.Android;

[Activity(
    Label = "Server Log",
    Exported = false,
    Theme = "@style/BlackActivityTheme",
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize |
                           ConfigChanges.Orientation |
                           ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize
)]
public class ServerActivity : BlackActivity
{
    private static readonly LocalizedText _titleText =
        UiText("Title", "Server Log");

    private static readonly LocalizedText _commandHintText =
        UiText("CommandHint", "Enter server command");

    private static readonly LocalizedText _executeText =
        UiText("Execute", "Run");

    private static readonly LocalizedText _stopText =
        UiText("Stop", "Stop");

    private static readonly LocalizedText _switchGuiText =
        UiText("SwitchGui", "Switch to GUI");

    private static readonly LocalizedText _stopConfirmTitleText =
        UiText("StopConfirmTitle", "Stop the server?");

    private static readonly LocalizedText _stopConfirmMessageText =
        UiText("StopConfirmMessage", "The current server process will exit.");

    private static readonly LocalizedText _guiConfirmTitleText =
        UiText("GuiConfirmTitle", "Switch to GUI mode?");

    private static readonly LocalizedText _guiConfirmMessageText =
        UiText(
            "GuiConfirmMessage",
            "The run mode will be saved and restarted in the graphical interface.");

    private static readonly LocalizedText _confirmText =
        UiText("Confirm", "Confirm");

    private static readonly LocalizedText _cancelText =
        UiText("Cancel", "Cancel");

    private const int _maxTextLength = 200_000;

    private const int _suggestionDebounceMs = 120;

    private const int _maxRenderedSuggestions = 32;

    private readonly Lock _logLock = new();

    private readonly StringBuilder _logBuilder = new();

    private ScrollView _scrollView = null!;

    private TextView _textView = null!;

    private ScrollView _suggestionsScrollView = null!;

    private LinearLayout _suggestionsContainer = null!;

    private EditText _commandInput = null!;

    private Button _commandButton = null!;

    private Button _executeButton = null!;

    private Button _stopButton = null!;

    private Button _guiButton = null!;

    private bool _stopRequested;

    private bool _destroyed;

    private bool _commandExecuting;

    private string _localizedLanguage = string.Empty;

    private bool _commandBrowseMode;

    private bool _suppressSuggestionRefresh;

    private int _suggestionRequestVersion;

    private Task<int>? _serverTask;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        PlatformManager.RegisterPlatform(Platform.Android);

        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            FocusableInTouchMode = true
        };
        layout.SetBackgroundColor(Color.Black);
        layout.SetPadding(0, 0, 0, GetNavigationBarHeight());

        var buttonsLayout = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };

        _stopButton = new Button(this)
        {
            Text = _stopText.Resolve()
        };
        _stopButton.Click += (_, _) => ConfirmAction(
            _stopConfirmTitleText.Resolve(),
            _stopConfirmMessageText.Resolve(),
            () => _ = ExecuteOperationCommandAsync("stop"));

        _guiButton = new Button(this)
        {
            Text = _switchGuiText.Resolve()
        };
        _guiButton.Click += (_, _) => ConfirmAction(
            _guiConfirmTitleText.Resolve(),
            _guiConfirmMessageText.Resolve(),
            () => _ = ExecuteOperationCommandAsync("runmode gui"));

        buttonsLayout.AddView(
            _stopButton,
            new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
        );
        buttonsLayout.AddView(
            _guiButton,
            new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
        );

        var commandLayout = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        _suggestionsScrollView = new ScrollView(this)
        {
            Visibility = ViewStates.Gone
        };
        _suggestionsContainer = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        _suggestionsScrollView.AddView(
            _suggestionsContainer,
            new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
        );
        _commandInput = new EditText(this)
        {
            Hint = _commandHintText.Resolve(),
            ImeOptions = ImeAction.Send
        };
        _commandInput.SetSingleLine();
        _commandInput.EditorAction += (_, args) =>
        {
            if (args.ActionId is not ImeAction.Send)
            {
                return;
            }

            args.Handled = true;
            _ = ExecuteInputCommandAsync();
        };
        _commandInput.TextChanged += (_, _) =>
        {
            if (!_suppressSuggestionRefresh)
            {
                ScheduleSuggestionRefresh(executeWhenComplete: false);
            }
        };
        _commandInput.FocusChange += (_, args) =>
        {
            if (args.HasFocus)
            {
                ActivateCommandBrowse();
                ShowKeyboard();
                ScheduleSuggestionRefresh(executeWhenComplete: false);
            }
        };
        _commandButton = new Button(this)
        {
            Text = "/"
        };
        _commandButton.Click += (_, _) => ToggleCommandBrowse();
        _executeButton = new Button(this)
        {
            Text = _executeText.Resolve()
        };
        _executeButton.Click += (_, _) => _ = ExecuteInputCommandAsync();
        commandLayout.AddView(
            _commandButton,
            new LinearLayout.LayoutParams(
                Dp(54),
                ViewGroup.LayoutParams.WrapContent)
        );
        commandLayout.AddView(
            _commandInput,
            new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
        );
        commandLayout.AddView(
            _executeButton,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent)
        );

        _scrollView = new ScrollView(this);
        _textView = new TextView(this)
        {
            TextSize = 12f,
            Typeface = Typeface.Monospace
        };
        _textView.MovementMethod = new ScrollingMovementMethod();
        _textView.SetTextIsSelectable(true);

        _scrollView.AddView(
            _textView,
            new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent
            )
        );

        layout.AddView(
            _scrollView,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                0,
                1f
            )
        );

        layout.AddView(
            _suggestionsScrollView,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent
            )
        );

        layout.AddView(
            commandLayout,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent
            )
        );

        layout.AddView(
            buttonsLayout,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent
            )
        );

        SetContentView(layout);
        layout.RequestFocus();
        AppendInitialLogs();
        Log.MsgAdded += OnLogMsgAdded;

        InitializeAndroidId();
        RunMode.Value = RunModeType.HeadlessServer;
        var gameArguments = Intent?.GetStringArrayExtra(MainActivity.gameArgumentsExtra) ?? [];
        var startup = StartupManager.Load(gameArguments);
        _serverTask = Task.Run(() => HeadlessEntry.Main(startup));
        _ = CompleteServerRunAsync(_serverTask);
        PollCommandConsoleState();
    }

    private int GetNavigationBarHeight()
    {
        var resourceId = Resources?.GetIdentifier("navigation_bar_height", "dimen", "android") ?? 0;
        return resourceId > 0 ? Resources!.GetDimensionPixelSize(resourceId) : 0;
    }

    private void InitializeAndroidId()
    {
        GetMachineID.AndroidID = AndroidProviderSettings.Secure
            .GetString(
                ContentResolver,
                AndroidProviderSettings.Secure.AndroidId
            ) ?? string.Empty;
    }

    protected override void OnDestroy()
    {
        _destroyed = true;
        Log.MsgAdded -= OnLogMsgAdded;
        if (!_stopRequested)
        {
            HeadlessEntry.RequestStop();
        }

        base.OnDestroy();
    }

    public override void OnBackPressed()
    {
        if (_commandBrowseMode ||
            _suggestionsScrollView.Visibility is ViewStates.Visible ||
            _commandInput.HasFocus)
        {
            CancelCommandBrowse();
            return;
        }

        ConfirmAction(
            _stopConfirmTitleText.Resolve(),
            _stopConfirmMessageText.Resolve(),
            () => _ = ExecuteOperationCommandAsync("stop"));
    }

    private async Task CompleteServerRunAsync(Task<int> serverTask)
    {
        await serverTask;
        if (_destroyed)
        {
            return;
        }

        RunOnUiThread(() =>
        {
            if (GameExitManager.ExitAction is GameExitAction.Restart)
            {
                SetResult((Result)MainActivity.restartResultCode);
            }
            else if (GameExitManager.ExitAction is GameExitAction.SwitchInstance)
            {
                SetResult((Result)MainActivity.switchInstanceResultCode);
            }
            else
            {
                SetResult((Result)MainActivity.exitResultCode);
            }

            Finish();
        });
    }

    private void ConfirmAction(string title, string message, Action onConfirmed)
    {
        RunOnUiThread(() =>
        {
            new AndroidAlertDialog.Builder(this)
                .SetTitle(title)?
                .SetMessage(message)?
                .SetPositiveButton(_confirmText.Resolve(), (_, _) => onConfirmed())?
                .SetNegativeButton(_cancelText.Resolve(), (_, _) => { })?
                .Show();
        });
    }

    private async Task ExecuteInputCommandAsync()
    {
        var input = _commandInput.Text?.Trim() ?? string.Empty;
        if (input.Length == 0)
        {
            return;
        }

        ResetCommandBrowse();
        await ExecuteCommandAsync(input);
    }

    private async Task ExecuteOperationCommandAsync(string input)
    {
        var result = await ExecuteCommandAsync(input);
        if (input.Equals("stop", StringComparison.OrdinalIgnoreCase) && result.Success)
        {
            _stopRequested = true;
        }
    }

    private async Task<CommandResult> ExecuteCommandAsync(string input)
    {
        if (_commandExecuting)
        {
            return CommandResult.LocalizedFail(
                "command.busy",
                "CommandRateLimited_Message",
                "The previous command is still running.");
        }

        _commandExecuting = true;
        UpdateCommandControls();
        AppendLogLine($"> {input}");
        CommandResult result;
        try
        {
            result = await HeadlessEntry.SubmitConsoleCommandAsync(input);
        }
        catch (Exception exception)
        {
            Log.Error(exception);
            result = CommandResult.Fail("command.failed", exception.Message);
        }
        finally
        {
            _commandExecuting = false;
        }

        if (!_destroyed)
        {
            RunOnUiThread(() =>
            {
                var level = result.Success ? "OK" : "ERROR";
                AppendLogLine($"COMMAND {level} [{result.Code}] {CommandText.Resolve(result)}");
                UpdateCommandControls();
            });
        }

        return result;
    }

    private void ScheduleSuggestionRefresh(bool executeWhenComplete)
    {
        var version = ++_suggestionRequestVersion;
        var input = _commandInput.Text ?? string.Empty;
        _ = RefreshSuggestionsAsync(input, version, executeWhenComplete);
    }

    private async Task RefreshSuggestionsAsync(
        string input,
        int version,
        bool executeWhenComplete)
    {
        await Task.Delay(_suggestionDebounceMs);
        if (_destroyed ||
            version != _suggestionRequestVersion ||
            !HeadlessEntry.IsCommandConsoleReady)
        {
            return;
        }

        var result = await HeadlessEntry.SubmitConsoleSuggestionsAsync(input);
        if (_destroyed || version != _suggestionRequestVersion)
        {
            return;
        }

        RunOnUiThread(() =>
        {
            if (_destroyed ||
                version != _suggestionRequestVersion ||
                !string.Equals(_commandInput.Text, input, StringComparison.Ordinal))
            {
                return;
            }

            if (executeWhenComplete && result.CanExecute && result.Items.Count == 0)
            {
                _ = ExecuteInputCommandAsync();
                return;
            }

            ShowSuggestions(result.Items);
        });
    }

    private void ShowSuggestions(IReadOnlyList<CommandSuggestion> suggestions)
    {
        _suggestionsContainer.RemoveAllViews();
        foreach (var suggestion in suggestions.Take(_maxRenderedSuggestions))
        {
            _suggestionsContainer.AddView(CreateSuggestionRow(suggestion));
        }

        if (_suggestionsContainer.ChildCount == 0)
        {
            HideSuggestions();
            return;
        }

        var visibleRows = Math.Min(_suggestionsContainer.ChildCount, 4);
        _suggestionsScrollView.LayoutParameters = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            Dp(visibleRows * 54));
        _suggestionsScrollView.Visibility = ViewStates.Visible;
    }

    private View CreateSuggestionRow(CommandSuggestion suggestion)
    {
        var row = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
            Clickable = true
        };
        row.SetBackgroundColor(Color.Rgb(38, 38, 38));
        row.SetPadding(Dp(12), Dp(5), Dp(12), Dp(5));
        row.Click += (_, _) => ApplySuggestion(suggestion);

        var value = new TextView(this)
        {
            Text = suggestion.Value,
            TextSize = 14f,
            Gravity = GravityFlags.CenterVertical
        };
        value.SetSingleLine();
        value.SetTextColor(Color.White);

        var description = new TextView(this)
        {
            Text = suggestion.Description,
            TextSize = 11f,
            Gravity = GravityFlags.CenterVertical
        };
        description.SetSingleLine();
        description.SetTextColor(Color.Rgb(180, 180, 180));

        row.AddView(
            value,
            new LinearLayout.LayoutParams(0, Dp(44), 0.42f));
        row.AddView(
            description,
            new LinearLayout.LayoutParams(0, Dp(44), 0.58f));

        var layoutParameters = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        layoutParameters.SetMargins(0, 0, 0, Dp(1));
        row.LayoutParameters = layoutParameters;
        return row;
    }

    private void ApplySuggestion(CommandSuggestion suggestion)
    {
        if (suggestion.Value.StartsWith('<'))
        {
            BeginCommandBrowse(showKeyboard: true);
            return;
        }

        var hadInputFocus = _commandInput.HasFocus;
        var value = suggestion.IsArgument
            ? CommandLineTokenizer.FormatToken(suggestion.Value)
            : suggestion.Value;
        var text = CommandLineTokenizer.ReplaceCurrentToken(
            _commandInput.Text ?? string.Empty,
            value) + " ";
        _suppressSuggestionRefresh = true;
        _commandInput.Text = text;
        _commandInput.SetSelection(text.Length);
        _suppressSuggestionRefresh = false;
        HideSuggestions();
        if (hadInputFocus)
        {
            _commandInput.RequestFocus();
        }

        ScheduleSuggestionRefresh(executeWhenComplete: true);
    }

    private void ToggleCommandBrowse()
    {
        if (_commandBrowseMode)
        {
            CancelCommandBrowse();
        }
        else
        {
            BeginCommandBrowse(showKeyboard: false);
            ScheduleSuggestionRefresh(executeWhenComplete: false);
        }
    }

    private void BeginCommandBrowse(bool showKeyboard)
    {
        ActivateCommandBrowse();
        if (!showKeyboard)
        {
            HideKeyboard();
            return;
        }

        if (!_commandInput.HasFocus)
        {
            _commandInput.RequestFocus();
            return;
        }

        ShowKeyboard();
    }

    private void ActivateCommandBrowse()
    {
        _commandBrowseMode = true;
        _commandButton.Text = "×";
        if (string.IsNullOrWhiteSpace(_commandInput.Text))
        {
            _suppressSuggestionRefresh = true;
            _commandInput.Text = "/";
            _commandInput.SetSelection(1);
            _suppressSuggestionRefresh = false;
        }
    }

    private void ShowKeyboard()
    {
        _commandInput.Post(() =>
        {
            if (GetSystemService(InputMethodService) is InputMethodManager inputMethodManager)
            {
                inputMethodManager.ShowSoftInput(_commandInput, ShowFlags.Implicit);
            }
        });
    }

    private void CancelCommandBrowse()
    {
        ResetCommandBrowse();
        HideKeyboard();
    }

    private void ResetCommandBrowse()
    {
        _commandBrowseMode = false;
        _commandButton.Text = "/";
        _suppressSuggestionRefresh = true;
        _commandInput.Text = string.Empty;
        _suppressSuggestionRefresh = false;
        _commandInput.ClearFocus();
        HideSuggestions();
    }

    private void HideKeyboard()
    {
        if (GetSystemService(InputMethodService) is InputMethodManager inputMethodManager)
        {
            inputMethodManager.HideSoftInputFromWindow(
                _commandInput.WindowToken,
                HideSoftInputFlags.None);
        }
    }

    private void HideSuggestions()
    {
        _suggestionRequestVersion++;
        _suggestionsContainer.RemoveAllViews();
        _suggestionsScrollView.Visibility = ViewStates.Gone;
    }

    private int Dp(int value)
    {
        return (int)MathF.Round(value * (Resources?.DisplayMetrics?.Density ?? 1f));
    }

    private void UpdateCommandControls()
    {
        if (_destroyed)
        {
            return;
        }

        var enabled = HeadlessEntry.IsCommandConsoleReady && !_commandExecuting;
        _commandButton.Enabled = enabled;
        _commandInput.Enabled = enabled;
        _executeButton.Enabled = enabled &&
                                 !string.IsNullOrWhiteSpace(
                                     (_commandInput.Text ?? string.Empty).Trim('/', ' '));
        _stopButton.Enabled = enabled;
        _guiButton.Enabled = enabled;
        var currentLanguage = LanguageManager.CurrentLanguage;
        if (HeadlessEntry.IsCommandConsoleReady &&
            !currentLanguage.Equals(_localizedLanguage, StringComparison.OrdinalIgnoreCase))
        {
            RefreshLocalizedText();
            _localizedLanguage = currentLanguage;
        }
    }

    private void PollCommandConsoleState()
    {
        if (_destroyed)
        {
            return;
        }

        UpdateCommandControls();
        _textView.PostDelayed(PollCommandConsoleState, 250);
    }

    private void RefreshLocalizedText()
    {
        Title = _titleText.Resolve();
        _commandInput.Hint = _commandHintText.Resolve();
        _executeButton.Text = _executeText.Resolve();
        _stopButton.Text = _stopText.Resolve();
        _guiButton.Text = _switchGuiText.Resolve();
    }

    private static LocalizedText UiText(string key, string fallback)
    {
        return new LocalizedText("AndroidServer", key, fallback);
    }

    private void AppendInitialLogs()
    {
        foreach (var line in GameLogSink.GetRecentLogLines(131072))
        {
            AppendLogLine(line);
        }
    }

    private void OnLogMsgAdded(string message)
    {
        RunOnUiThread(() => { AppendLogLine(message); });
    }

    private void AppendLogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lock (_logLock)
        {
            _logBuilder.AppendLine(line);
            if (_logBuilder.Length > _maxTextLength)
            {
                var text = _logBuilder.ToString();
                _logBuilder.Clear();
                _logBuilder.Append(text[^(_maxTextLength / 2)..]);
            }

            _textView.Text = _logBuilder.ToString();
            _scrollView.Post(() => _scrollView.FullScroll(FocusSearchDirection.Down));
        }
    }
}
