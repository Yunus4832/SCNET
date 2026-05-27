using System.Xml.Linq;

using Engine.Graphics;

namespace Game.Screens;

public class GameScreen : Screen
{
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

        ShowHideCursors(
            DialogsManager.HasDialogs(this) ||
            DialogsManager.HasDialogs(RootWidget) ||
            ScreensManager.CurrentScreen != this
        );
    }

    public override void Draw(DrawContext dc)
    {
        if (!ScreensManager.IsAnimating && SettingsManager.ResolutionMode == ResolutionMode.High)
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
}
