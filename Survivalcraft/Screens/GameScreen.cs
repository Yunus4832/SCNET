using System.Xml.Linq;

using Engine.Graphics;

using EntitySystem.Core;

using Game.Application;
using Game.Commands;
using Game.Commands.BuiltIn;

namespace Game.Screens;

public class GameScreen : Screen
{
    private Project? _administrationDialogProject;

    public GameScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/GameScreen");
        LoadContents(this, node);
        IsDrawRequired = true;
        Window.Deactivated += delegate
        {
            if (!Window.IsClosing) { ClientApplication.World?.RequestSave(false, false); }
        };
    }

    public override void Enter(object[] parameters)
    {
        if (ClientApplication.World?.Project is null)
        {
            throw new InvalidOperationException("ClientApplication.World?.Project is not initialized");
        }

        ClientApplication.World?.Project.FindSubsystem<SubsystemAudio>(true)!.Unmute();
        MusicManager.CurrentMix = MusicManager.Mix.None;
        TryShowAdministrationBootstrap();
    }

    public override void Leave()
    {
        if (ClientApplication.World?.Project is not null)
        {
            ClientApplication.World?.Project.FindSubsystem<SubsystemAudio>(true)!.Mute();
        }

        ShowHideCursors(true);
        MusicManager.CurrentMix = MusicManager.Mix.Menu;
    }


    public override void Update()
    {
        if (GameManager.Runtime?.Client is { IsClosed: true })
        {
            ClientApplication.World?.RequestLeave();
            return;
        }

        TryShowAdministrationBootstrap();

        ShowHideCursors(
            DialogsManager.HasDialogs(this) ||
            DialogsManager.HasDialogs(RootWidget) ||
            DialogsManager.HasDialogs(
                ClientApplication.World?.LocalCharacter?.Entity.FindComponent<ComponentCharacterPresentation>(true)
                    ?.GuiWidget
            ) ||
            ScreensManager.CurrentScreen != this
        );
    }

    public override void Draw(DrawContext dc)
    {
        if (!ScreensManager.IsAnimating &&
            Configuration.ClientConfiguration.Preferences.ResolutionMode == ResolutionMode.High)
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
        if (GameManager.Runtime is not { IsMultiplayer: true, Authority.Project: { } project } ||
            ReferenceEquals(_administrationDialogProject, project) ||
            ServerAdministrationBootstrap.IsClaimed(project) ||
            ClientApplication.World?.LocalCharacter is not { } player ||
            !ServerAdministrationBootstrap.TryGetClaimCode(project, out var code))
        {
            return;
        }

        _administrationDialogProject = project;
        var claimCommand = $"/auth claim {code}";
        DialogsManager.ShowDialog(
            player.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget,
            new MessageDialog(
                CommandText.Get(
                    "AuthDialogTitle",
                    "服务器管理员尚未认领"
                ),
                CommandText.Get(
                    "AuthDialogBody",
                    "认领码：{0}\n\n认领只会授予标准权限的管理和再授权能力，不包含停服等控制台权限。",
                    code
                ),
                CommandText.Get(
                    "AuthDialogClaim",
                    "立即认领"
                ),
                CommandText.Get(
                    "AuthDialogCopy",
                    "复制命令"
                ),
                new Vector2(680f, -1f),
                MessageDialog.CancelBehavior.Dismiss,
                button =>
                {
                    if (button is MessageDialogButton.Button2)
                    {
                        ClipboardManager.ClipboardString = claimCommand;
                        DisplayToast(
                            player.Project,
                            CommandText.Get(
                                "AuthDialogCopied",
                                "已复制：{0}",
                                claimCommand
                            ),
                            success: true
                        );
                        return;
                    }

                    CommandGateway.Submit(player.CharacterData, claimCommand);
                }
            )
        );
    }

    private static void DisplayToast(
        Project project,
        string message,
        bool success
    )
    {
        project.FindSubsystem<SubsystemMessages>(true)!.Service.DisplayLocal(
            GameMessage.System(
                message,
                success ? GameMessageTone.Success : GameMessageTone.Error,
                GameMessagePresentation.Toast
            )
        );
    }
}
