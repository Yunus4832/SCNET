namespace Game;

[Conditional("DEBUG")]
public class DebugMenuItemAttribute(double step) : DebugItemAttribute
{
    public double Step = step;

    public DebugMenuItemAttribute() : this(1.0)
    {
    }
}
