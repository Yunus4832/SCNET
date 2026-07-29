using System.Xml.Linq;

using Engine.Graphics;

using EntitySystem.Core;

using Game.Commands;
using Game.Network;
using Game.Network.Enums;

namespace Game.Screens;

public class GameScreen : Screen
{
    private Project? _administrationDialogProject;

    private double _lastAutosaveTime;

    public GameScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/GameScreen");
        LoadContents(this, node);
        IsDrawRequired = true;
        Window.Deactivated += delegate { GameManager.SaveProject(true, false); };
    }

    public override void Enter(object[] parameters)
    {
        if (GameManager.Project is null)
        {
            throw new InvalidOperationException("GameManager.Project is not initialized");
        }

        GameManager.Project.FindSubsystem<SubsystemAudio>(true)!.Unmute();
        MusicManager.CurrentMix = MusicManager.Mix.None;
        _administrationDialogProject = null;
        TryShowAdministrationBootstrap();
    }

    public override void Leave()
    {
        if (GameManager.Project is not null)
        {
            GameManager.Project.FindSubsystem<SubsystemAudio>(true)!.Mute();
            GameManager.SaveProject(true, true);
        }

        ShowHideCursors(true);
        MusicManager.CurrentMix = MusicManager.Mix.Menu;
    }

    public override void Update()
    {
        var realTime = Time.RealTime;
        if (realTime - _lastAutosaveTime > 120.0)
        {
            _lastAutosaveTime = realTime;
            GameManager.SaveProject(false, true);
        }

        GameManager.UpdateProject();
        TryShowAdministrationBootstrap();

        ShowHideCursors(
            DialogsManager.HasDialogs(this) ||
            DialogsManager.HasDialogs(RootWidget) ||
            ScreensManager.CurrentScreen != this
        );
    }

    public override void Draw(DrawContext dc)
    {
        if (!ScreensManager.IsAnimating && SettingsManager.Current.ResolutionMode == ResolutionMode.High)
        {
            Display.Clear(Color.Black, 1f, 0);
        }
    }

    public void ShowHideCursors(bool show)
    {
        Input.IsMouseCursorVisible = show;
        Input.IsPadCursorVisible = show;
        Input.IsVrCursorVisible = show;
    }

    private void TryShowAdministrationBootstrap()
    {
        if (RunMode.Value is not RunModeType.Gui ||
            CommonLib.WorkType is not WorkType.Server ||
            GameManager.Project is not { } project ||
            ReferenceEquals(_administrationDialogProject, project) ||
            ServerAdministrationBootstrap.IsClaimed(project) ||
            CommonLib.MainPlayer is not { } player ||
            !ServerAdministrationBootstrap.TryGetClaimCode(project, out var code))
        {
            return;
        }

        _administrationDialogProject = project;
        var claimCommand = $"/auth claim {code}";
        DialogsManager.ShowDialog(
            this,
            new MessageDialog(
                CommandText.Get(
                    "AuthDialogTitle",
                    "服务器管理员尚未认领"),
                CommandText.Get(
                    "AuthDialogBody",
                    "认领码：{0}\n\n认领只会授予标准权限的管理和再授权能力，不包含停服等控制台权限。",
                    code),
                CommandText.Get(
                    "AuthDialogClaim",
                    "以当前玩家认领"),
                CommandText.Get(
                    "AuthDialogCopy",
                    "复制并稍后处理"),
                button =>
                {
                    if (button is MessageDialogButton.Button2)
                    {
                        ClipboardManager.ClipboardString = claimCommand;
                        DialogsManager.Alert(CommandText.Get(
                            "AuthDialogCopied",
                            "已复制：{0}",
                            claimCommand));
                        return;
                    }

                    var result = CommandExecutor.ExecutePlayer(claimCommand, player.PlayerData);
                    DialogsManager.Alert(CommandText.Resolve(result));
                }));
    }
}
