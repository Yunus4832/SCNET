using System.Xml.Linq;

using Engine.Audio;
using Engine.Graphics;

using Game.Network.Serialization;

namespace Game.Screens;

public class LoadingScreen : Screen
{
    public enum LogType
    {
        Info,
        Warning,
        Error,
        Advice
    }

    private static readonly ListPanelWidget _logList = new()
        { Direction = LayoutDirection.Vertical, PlayClickSound = false };

    private readonly RectangleWidget _background = new()
    {
        FillColor = SettingsManager.DisplayLog ? Color.Black : Color.White, OutlineThickness = 0f,
        DepthWriteEnabled = true
    };

    private readonly CanvasWidget _canvas = new();

    private readonly List<Action> _loadingActions = [];

    private readonly List<Action> _modLoadingActions = [];

    static LoadingScreen()
    {
        _logList.ItemWidgetFactory = obj =>
        {
            if (obj is not LogItem logItem)
            {
                throw new ArgumentNullException(nameof(logItem));
            }

            var canvasWidget = new CanvasWidget
            {
                Size = new Vector2(Display.Viewport.Width, 40), Margin = new Vector2(0, 2),
                HorizontalAlignment = WidgetAlignment.Near
            };
            var fontTextWidget = new FontTextWidget
            {
                FontScale = 0.6f, Text = logItem.Message, Color = GetColor(logItem.LogType),
                VerticalAlignment = WidgetAlignment.Center, HorizontalAlignment = WidgetAlignment.Near
            };
            canvasWidget.Children.Add(fontTextWidget);
            canvasWidget.IsVisible = SettingsManager.DisplayLog;
            _logList.IsEnabled = SettingsManager.DisplayLog;
            return canvasWidget;
        };
        _logList.ItemSize = 30;
    }

    public LoadingScreen()
    {
        _canvas.Size = new Vector2(float.PositiveInfinity);
        _canvas.AddChildren(_background);
        _canvas.AddChildren(_logList);
        AddChildren(_canvas);
        Info("Initializing Mods Manager. Api Version: " + ModsManager.ApiVersion);
    }

    public static Color GetColor(LogType type)
    {
        return type switch
        {
            LogType.Advice => Color.Cyan,
            LogType.Error => Color.Red,
            LogType.Warning => Color.Yellow,
            _ => Color.White
        };
    }

    public void ContentLoaded()
    {
        if (SettingsManager.DisplayLog)
        {
            return;
        }

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
        _canvas.AddChildren(_logList);
        AddChildren(_canvas);
    }

    public static void Error(string msg)
    {
        Add(LogType.Error, "[Error]" + msg);
    }

    public static void Info(string msg)
    {
        Add(LogType.Info, "[Info]" + msg);
    }

    public static void Warning(string msg)
    {
        Add(LogType.Warning, "[Warning]" + msg);
    }

    public static void Advice(string msg)
    {
        Add(LogType.Advice, "[Advice]" + msg);
    }

    public static void Add(LogType type, string msg)
    {
        Dispatcher.Dispatch(delegate
        {
            var item = new LogItem(type, msg);
            _logList.AddItem(item);
            switch (type)
            {
                case LogType.Info:
                case LogType.Advice: Log.Information(msg); break;
                case LogType.Error: Log.Error(msg); break;
                case LogType.Warning: Log.Warning(msg); break;
            }

            _logList.ScrollToItem(item);
        });
    }

    private void InitActions()
    {
        AddLoadAction(delegate
        {
            //将所有的有效的 scmod 读取为ModEntity，并自动添加 SurvivalCraftModEntity
            ContentManager.Initialize();
            ModsManager.Initialize();
        });
        AddLoadAction(ContentLoaded);

        AddLoadAction(delegate
        {
            //检查所有Mod依赖项
            //根据加载顺序排序后的结果
            ModsManager.ModList.Clear();
            foreach (var item in ModsManager.ModListAll)
            {
                if (item.IsDependencyChecked)
                {
                    continue;
                }

                item.CheckDependencies(ModsManager.ModList);
            }

            foreach (var item in ModsManager.ModListAll)
            {
                item.IsDependencyChecked = false;
            }
        });
        AddLoadAction(delegate
        {
            //初始化所有ModEntity的语言包
            // 初始化语言列表
            var axa = ContentManager.List("Lang");
            LanguageControl.LanguageTypes.Clear();
            foreach (var contentInfo in axa)
            {
                var px = Path.GetFileNameWithoutExtension(contentInfo.Filename);
                if (!LanguageControl.LanguageTypes.Contains(px))
                {
                    LanguageControl.LanguageTypes.Add(px);
                }
            }

            if (ModsManager.Configs.ContainsKey("Language") &&
                LanguageControl.LanguageTypes.Contains(ModsManager.Configs["Language"]))
            {
                LanguageControl.Initialize(ModsManager.Configs["Language"]);
            }
            else
                // 如果不支持系統語言，英語是最佳選擇
            {
                LanguageControl.Initialize("zh-CN");
            }

            ModsManager.ModListAllDo(modEntity => { modEntity.LoadLanguage(); });
            LanguageControl.RefreshCommonWords();
        });
        AddLoadAction(PackageManager.Initialize);
        AddLoadAction(delegate
        {
            //读取所有的ModEntity的dll，并分离出ModLoader，保存Blocks
            ModsManager.ModListAllDo(modEntity => { modEntity.LoadDll(); });
        });
        AddLoadAction(delegate
        {
            Info("执行初始化任务");
            var actions = new List<Action>();
            ModsManager.ModListAllDo(modEntity => { modEntity.Loader?.OnLoadingStart(actions); });
            foreach (var ac in actions)
            {
                _modLoadingActions.Add(ac);
            }
        });
        AddLoadAction(delegate
        {
            //初始化TextureAtlas
            Info("初始化纹理地图");
            TextureAtlasManager.Initialize();
        });
        AddLoadAction(delegate
        {
            //初始化Database
            try
            {
                DatabaseManager.Initialize();
                ModsManager.ModListAllDo(modEntity => { modEntity.LoadXdb(ref DatabaseManager.DatabaseNodeField); });
            }
            catch (Exception e)
            {
                Warning(e.Message);
            }
        });
        AddLoadAction(delegate
        {
            Info("读取数据库");
            try
            {
                if (DatabaseManager.DatabaseNodeField == null)
                {
                    return;
                }

                DatabaseManager.LoadDataBaseFromXml(DatabaseManager.DatabaseNodeField);
            }
            catch (Exception e)
            {
                Warning(e.Message);
            }
        });
        AddLoadAction(delegate
        {
            //初始化方块管理器
            Info("初始化方块管理器");
            BlocksManager.Initialize();
        });
        AddLoadAction(CraftingRecipesManager.Initialize);
        InitScreens();
        AddLoadAction(delegate
        {
            BlocksTexturesManager.Initialize();
            CharacterSkinsManager.Initialize();
            CommunityContentManager.Initialize();
            ExternalContentManager.Initialize();
            FurniturePacksManager.Initialize();
            LightingManager.Initialize();
            MotdManager.Initialize();
            VersionsManager.Initialize();
            WorldsManager.Initialize();
        });
        AddLoadAction(delegate
        {
            Info("初始化Mod设置参数");
            if (!Storage.FileExists(ModsManager.ModsSettingPath))
            {
                return;
            }

            using var stream = Storage.OpenFile(ModsManager.ModsSettingPath, OpenFileMode.Read);
            try
            {
                var element = XElement.Load(stream);
                ModsManager.LoadModSettings(element);
            }
            catch (Exception e)
            {
                Warning(e.Message);
            }
        });
        AddLoadAction(delegate
        {
            ModsManager.ModListAllDo(modEntity =>
            {
                Info("等待剩下的任务完成:" + modEntity.ModInfo.PackageName);
                modEntity.Loader?.OnLoadingFinished(_modLoadingActions);
            });
        });
        AddLoadAction(delegate
        {
            ScreensManager.SwitchScreen("MainMenu");
            //如果音频库加载失败，则禁止声音播放
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
        AddLoadAction(delegate { AddScreen("ModifyWorld", new ModifyWorldScreen()); });
        AddLoadAction(delegate { AddScreen("WorldOptions", new WorldOptionsScreen()); });
        AddLoadAction(delegate { AddScreen("GameLoading", new GameLoadingScreen()); });
        AddLoadAction(delegate { AddScreen("Game", new GameScreen()); });
        AddLoadAction(delegate { AddScreen("ExternalContent", new ExternalContentScreen()); });
        AddLoadAction(delegate { AddScreen("CommunityContent", new CommunityContentScreen()); });
        AddLoadAction(delegate { AddScreen("Content", new ContentScreen()); });
        AddLoadAction(delegate { AddScreen("ManageContent", new ManageContentScreen()); });
        AddLoadAction(delegate { AddScreen("ModsManageContent", new ModsManageContentScreen()); });
        AddLoadAction(delegate { AddScreen("ManageUser", new ManageUserScreen()); });
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
        _logList.ClearItems();
        Window.VSync = SettingsManager.VSync;
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
            DialogsManager.ShowDialog(null, new MessageDialog(LanguageControl.Warning, "Quit?", LanguageControl.Ok,
                LanguageControl.No, vt =>
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

        if (!ModsManager.GetAllowContinue())
        {
            return;
        }

        if (_modLoadingActions.Count > 0)
        {
            try
            {
                _modLoadingActions[0].Invoke();
            }
            catch (Exception e)
            {
                Error(e.Message);
            }
            finally
            {
                _modLoadingActions.RemoveAt(0);
            }
        }
        else
        {
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
                Error(e.Message);
            }
            finally
            {
                _loadingActions.RemoveAt(0);
            }
        }
    }

    private class LogItem(LogType type, string log)
    {
        public readonly LogType LogType = type;

        public readonly string Message = log;
    }
}
