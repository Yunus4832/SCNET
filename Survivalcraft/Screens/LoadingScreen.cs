using Engine.Audio;
using Engine.Graphics;

using Game.Application;

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
    }

    public void ContentLoaded()
    {
        ClearChildren();
        var rectangle1 = new RectangleWidget
        {
            FillColor = Color.White,
            OutlineColor = Color.Transparent,
            Size = new Vector2(256f),
            VerticalAlignment = WidgetAlignment.Center,
            HorizontalAlignment = WidgetAlignment.Center
        };
        rectangle1.Subtexture = AssetManager.Get<Subtexture>("Textures/Branding/PublisherLogo");
        var rectangle2 = new RectangleWidget
        {
            FillColor = Color.White,
            OutlineColor = Color.Transparent,
            Size = new Vector2(80, 50),
            VerticalAlignment = WidgetAlignment.Far,
            HorizontalAlignment = WidgetAlignment.Far,
            Margin = new Vector2(10f)
        };
        rectangle2.Subtexture = AssetManager.Get<Subtexture>("Textures/Branding/EngineLogo");
        var busyBar = new BusyBarWidget
        {
            VerticalAlignment = WidgetAlignment.Far,
            HorizontalAlignment = WidgetAlignment.Center,
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
        AddLoadAction(
            delegate
            {
                var startupSession = StartupManager.Current.Session;
                var plan = StartupModProfileBootstrapper.PrepareLoadPlan(
                    startupSession,
                    Storage.GetSystemPath(ClientApplication.Paths.ContentPackageCache),
                    Log.Information
                );
                Log.Information("初始化模组运行时");
                GameEntry.SetModRuntime(
                    GameModRuntime.StartFromPlan(
                        plan,
                        RuntimeSide.Client,
                        Commands.BuiltIn.BuiltInApplicationCommands.Register
                    )
                );
                AssetManager.Initialize();
                GameEntry.InitializeClientContent(CurrentModRuntime.Value!);
                LabelWidget.BitmapFont = AssetManager.Get<BitmapFont>("Fonts/Pericles");
            }
        );
        AddLoadAction(ContentLoaded);
        AddLoadAction(
            delegate
            {
                Log.Information("初始化语言资源");
                ClientApplication.Content.PublishLanguage(
                    CurrentModRuntime.Value!.Content.CreateLanguageCatalog(
                        AppConfigManager.Values.TryGetValue("Language", out var language) ? language : "zh-CN"
                    )
                );
            }
        );
        AddLoadAction(
            delegate
            {
                Log.Information("初始化纹理图集");
                TextureAtlasManager.Initialize();
            }
        );
        AddLoadAction(
            delegate
            {
                try
                {
                    Log.Information("初始化游戏数据库");
                    CurrentModRuntime.Value!.InitializeContentData();
                    Log.Information("初始化客户端预览");
                    GameEntry.InitializeClientPreview(CurrentModRuntime.Value!);
                }
                catch (Exception e)
                {
                    Log.Warning(e.Message);
                }
            }
        );
        InitScreens();
        AddLoadAction(
            delegate
            {
                BlocksTexturesManager.Initialize();
                CharacterSkinsManager.Initialize();
                FurniturePacksManager.Initialize();
                LightingManager.Initialize();
                ClientApplication.Worlds.SetNewWorldNames(
                    AssetManager.Get<string>("NewWorldNames")
                        .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                );
            }
        );
        AddLoadAction(
            delegate
            {
                if (!SessionInfoManager.TryRestoreGuiSession())
                {
                    ScreensManager.SwitchScreen("MainMenu");
                }

                // 如果音频库加载失败，则禁止声音播放
                if (!Mixer.IsAudioInitialized)
                {
                    DialogsManager.Alert(Localization.ClientLanguage.Catalog.Get("LoadingScreen", "AudioUnavailable"));
                }

                MusicManager.CurrentMix = MusicManager.Mix.Menu;
                GameInformationManager.StartAutomaticCheck();
            }
        );
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
        AddLoadAction(delegate { AddScreen("SettingsDiagnostics", new SettingsDiagnosticsScreen()); });
        AddLoadAction(delegate { AddScreen("SettingsAudio", new SettingsAudioScreen()); });
        AddLoadAction(delegate { AddScreen("SettingsControls", new SettingsControlsScreen()); });
        AddLoadAction(delegate { AddScreen("ManageIdentities", new ManageIdentitiesScreen()); });
        AddLoadAction(delegate { AddScreen("Play", new PlayScreen()); });
        AddLoadAction(delegate { AddScreen("NewWorld", new NewWorldScreen()); });
        AddLoadAction(delegate { AddScreen("WorldServerSettings", new WorldServerSettingsScreen()); });
        AddLoadAction(delegate { AddScreen("ModifyWorld", new ModifyWorldScreen()); });
        AddLoadAction(delegate { AddScreen("WorldOptions", new WorldOptionsScreen()); });
        AddLoadAction(delegate { AddScreen("GameLoading", new GameLoadingScreen()); });
        AddLoadAction(delegate { AddScreen("HostedWorld", new HostedWorldScreen()); });
        AddLoadAction(delegate { AddScreen("Game", new GameScreen()); });
        AddLoadAction(delegate { AddScreen("ServiceCatalog", new ServiceCatalogScreen()); });
        AddLoadAction(delegate { AddScreen("ManageServices", new ManageServicesScreen()); });
        AddLoadAction(delegate { AddScreen("Content", new ContentScreen()); });
        AddLoadAction(delegate { AddScreen("OnlineContent", new OnlineContentScreen()); });
        AddLoadAction(delegate { AddScreen("ContentRepositories", new ContentRepositoryScreen()); });
        AddLoadAction(delegate { AddScreen("ContentPackages", new ContentPackageScreen()); });
        AddLoadAction(delegate { AddScreen("ModManagement", new ModManagementScreen()); });
        AddLoadAction(delegate { AddScreen("WorldMods", new WorldModsScreen()); });
        AddLoadAction(delegate { AddScreen("ManageContent", new ManageContentScreen()); });
        AddLoadAction(delegate { AddScreen("Character", new CharacterScreen()); });
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
        Window.VSync = Configuration.ClientConfiguration.Preferences.VSync;
        AssetManager.Dispose("Textures/Branding/PublisherLogo");
        AssetManager.Dispose("Textures/Branding/EngineLogo");
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
            DialogsManager.ShowDialog(
                null,
                new MessageDialog(
                    Localization.ClientLanguage.Resolver.Get("Usual", "warning", "Warning"),
                    Localization.ClientLanguage.Resolver.Get("LoadingScreen", "ConfirmQuit", "Quit the game?"),
                    Localization.ClientLanguage.Resolver.Get("Usual", "ok", "OK"),
                    Localization.ClientLanguage.Resolver.Get("Usual", "no", "No"),
                    vt =>
                    {
                        if (vt == MessageDialogButton.Button1)
                        {
                            Window.Close();
                        }
                        else
                        {
                            DialogsManager.HideAllDialogs();
                        }
                    }
                )
            );
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
