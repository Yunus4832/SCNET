using Engine.Audio;
using Engine.Media;

using Game.Network.Serialization;

namespace Game.Screens;

public class LoadingScreen : Screen
{
    private readonly RectangleWidget _background = new()
    {
        FillColor = Color.White,
        OutlineThickness = 0f,
        DepthWriteEnabled = true
    };

    private readonly CanvasWidget _canvas = new();

    private readonly List<Action> _loadingActions = [];

    public LoadingScreen()
    {
        _canvas.Size = new Vector2(float.PositiveInfinity);
        _canvas.AddChildren(_background);
        AddChildren(_canvas);
        Log.Information("Initializing Mods Manager. Api Version: " + ModPlatformInfo.ApiVersion);
    }

    public void ContentLoaded()
    {
        ClearChildren();
        var rectangle1 = new RectangleWidget
        {
            FillColor = Color.White, OutlineColor = Color.Transparent, Size = new Vector2(256f),
            VerticalAlignment = WidgetAlignment.Center, HorizontalAlignment = WidgetAlignment.Center
        };
        rectangle1.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/CandyRufusLogo");
        var rectangle2 = new RectangleWidget
        {
            FillColor = Color.White, OutlineColor = Color.Transparent, Size = new Vector2(80, 50),
            VerticalAlignment = WidgetAlignment.Far, HorizontalAlignment = WidgetAlignment.Far,
            Margin = new Vector2(10f)
        };
        rectangle2.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/EngineLogo");
        var busyBar = new BusyBarWidget
        {
            VerticalAlignment = WidgetAlignment.Far, HorizontalAlignment = WidgetAlignment.Center,
            Margin = new Vector2(0, 40)
        };
        _canvas.AddChildren(_background);
        _canvas.AddChildren(rectangle1);
        _canvas.AddChildren(rectangle2);
        _canvas.AddChildren(busyBar);
        AddChildren(_canvas);
    }

    private void InitActions()
    {
        AddLoadAction(delegate
        {
            var startupSession = StartupManager.Current.Session;
            if (StartupModProfileBootstrapper.EnsureStartupSessionProfile(
                    StartupManager.Current.Session.SessionId,
                    startupSession,
                    Storage.GetSystemPath(GamePaths.ContentPackageCache),
                    Log.Information))
            {
                return;
            }

            Log.Information("初始化模组运行时");
            ContentManager.Initialize();
            var profile = ModProfileManager.LoadEffectiveProfile(
                StartupManager.Current.Session.SessionId,
                startupSession
            );
            GameEntry.SetModRuntime(GameModRuntime.StartFromProfile(
                    profile,
                    Storage.GetSystemPath(GamePaths.ContentPackageCache),
                    ModSide.Client,
                    Log.Information
                )
            );
            CurrentModRuntime.Value!.InitializeAssets();
            LabelWidget.BitmapFont = ContentManager.Get<BitmapFont>("Fonts/Pericles");
        });
        AddLoadAction(ContentLoaded);
        AddLoadAction(delegate
        {
            Log.Information("初始化语言资源");
            CurrentModRuntime.Value!.InitializeLanguage(
                AppConfigStore.Values.TryGetValue("Language", out var language) ? language : "zh-CN");
        });
        AddLoadAction(PackageManager.Initialize);
        AddLoadAction(delegate
        {
            // 初始化TextureAtlas
            Log.Information("初始化纹理地图");
            TextureAtlasManager.Initialize();
        });
        AddLoadAction(delegate
        {
            Log.Information("初始化内置内容数据");
            try
            {
                CurrentModRuntime.Value!.InitializeContentData();
            }
            catch (Exception e)
            {
                Log.Warning(e.Message);
            }
        });
        InitScreens();
        AddLoadAction(delegate
        {
            BlocksTexturesManager.Initialize();
            CharacterSkinsManager.Initialize();
            FurniturePacksManager.Initialize();
            LightingManager.Initialize();
            WorldsManager.Initialize();
        });
        AddLoadAction(delegate
        {
            if (!SessionInfoManager.TryRestoreGuiSession())
            {
                ScreensManager.SwitchScreen("MainMenu");
            }

            // 如果音频库加载失败，则禁止声音播放
            if (!Mixer.IsAudioInitialized)
            {
                DialogsManager.Alert("音频系统加载失败，设备播放声音可能出现问题");
            }

            MusicManager.CurrentMix = MusicManager.Mix.Menu;
            GameUpdateHelper.CheckGameUpdate();
        });
    }

    private void InitScreens()
    {
        AddLoadAction(delegate { AddScreen("MainMenu", new MainMenuScreen()); });
        AddLoadAction(delegate { AddScreen("InstanceManagement", new InstanceManagementScreen()); });
        AddLoadAction(delegate { AddScreen("Recipaedia", new RecipaediaScreen()); });
        AddLoadAction(delegate { AddScreen("RecipaediaRecipes", new RecipaediaRecipesScreen()); });
        AddLoadAction(delegate { AddScreen("RecipaediaDescription", new RecipaediaDescriptionScreen()); });
        AddLoadAction(delegate { AddScreen("Bestiary", new BestiaryScreen()); });
        AddLoadAction(delegate { AddScreen("BestiaryDescription", new BestiaryDescriptionScreen()); });
        AddLoadAction(delegate { AddScreen("Help", new HelpScreen()); });
        AddLoadAction(delegate { AddScreen("HelpTopic", new HelpTopicScreen()); });
        AddLoadAction(delegate { AddScreen("Settings", new SettingsScreen()); });
        AddLoadAction(delegate { AddScreen("SettingsPerformance", new SettingsPerformanceScreen()); });
        AddLoadAction(delegate { AddScreen("SettingsGraphics", new SettingsGraphicsScreen()); });
        AddLoadAction(delegate { AddScreen("SettingsUi", new SettingsUiScreen()); });
        AddLoadAction(delegate { AddScreen("SettingsCompatibility", new SettingsCompatibilityScreen()); });
        AddLoadAction(delegate { AddScreen("SettingsAudio", new SettingsAudioScreen()); });
        AddLoadAction(delegate { AddScreen("SettingsControls", new SettingsControlsScreen()); });
        AddLoadAction(delegate { AddScreen("Play", new PlayScreen()); });
        AddLoadAction(delegate { AddScreen("NewWorld", new NewWorldScreen()); });
        AddLoadAction(delegate { AddScreen("WorldServerSettings", new WorldServerSettingsScreen()); });
        AddLoadAction(delegate { AddScreen("ModifyWorld", new ModifyWorldScreen()); });
        AddLoadAction(delegate { AddScreen("WorldOptions", new WorldOptionsScreen()); });
        AddLoadAction(delegate { AddScreen("GameLoading", new GameLoadingScreen()); });
        AddLoadAction(delegate { AddScreen("Game", new GameScreen()); });
        AddLoadAction(delegate { AddScreen("Content", new ContentScreen()); });
        AddLoadAction(delegate { AddScreen("ContentServer", new ContentServerScreen()); });
        AddLoadAction(delegate { AddScreen("ContentPackages", new ContentPackageScreen()); });
        AddLoadAction(delegate { AddScreen("ModManagement", new ModManagementScreen()); });
        AddLoadAction(delegate { AddScreen("ManageContent", new ManageContentScreen()); });
        AddLoadAction(delegate { AddScreen("Players", new PlayersScreen()); });
        AddLoadAction(delegate { AddScreen("Player", new PlayerScreen()); });
        AddLoadAction(delegate { AddScreen("NetPlay", new NetPlayScreen()); });
    }

    public void AddScreen(string name, Screen screen)
    {
        ScreensManager.AddScreen(name, screen);
    }

    private void AddLoadAction(Action action)
    {
        _loadingActions.Add(action);
    }

    public override void Leave()
    {
        Window.VSync = SettingsManager.Current.VSync;
        ContentManager.Dispose("Textures/Gui/CandyRufusLogo");
        ContentManager.Dispose("Textures/Gui/EngineLogo");
    }

    public override void Enter(object[] parameters)
    {
        Window.VSync = false;
        var remove = new List<string>();
        foreach (var screen in ScreensManager.Screens)
        {
            if (screen.Value == this)
            {
                continue;
            }

            remove.Add(screen.Key);
        }

        foreach (var screen in remove)
        {
            ScreensManager.Screens.Remove(screen);
        }

        InitActions();
        base.Enter(parameters);
    }

    public override void Update()
    {
        if (Input.Back || Input.Cancel)
        {
            DialogsManager.ShowDialog(null, new MessageDialog(LanguageManager.Warning, "Quit?", LanguageManager.Ok,
                LanguageManager.No, vt =>
                {
                    if (vt == MessageDialogButton.Button1)
                    {
                        Window.Close();
                    }
                    else
                    {
                        DialogsManager.HideAllDialogs();
                    }
                }));
        }

        if (_loadingActions.Count <= 0)
        {
            return;
        }

        try
        {
            _loadingActions[0].Invoke();
        }
        catch (Exception e)
        {
            ExceptionManager.ReportExceptionToUser("Startup failed.", e);
            _loadingActions.Clear();
        }
        finally
        {
            if (_loadingActions.Count > 0)
            {
                _loadingActions.RemoveAt(0);
            }
        }
    }
}
