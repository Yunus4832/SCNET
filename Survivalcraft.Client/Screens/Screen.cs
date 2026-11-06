namespace Game.Screens;

public class Screen : CanvasWidget
{
    public static Action Init = Actions.Empty;

    public Screen()
    {
        Init.Invoke();
    }

    public virtual void Enter(object[] parameters)
    {
    }

    public virtual void Leave()
    {
    }
}
