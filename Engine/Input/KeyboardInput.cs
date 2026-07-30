namespace Engine.Input;

public class KeyboardInput
{
    public static readonly List<char> Chars = [];

    public static bool BackspacePressed
    {
        get
        {
            var pressed = field;
            field = false;
            return pressed;
        }
        set;
    }

    public static bool DeletePressed
    {
        get
        {
            var pressed = field;
            field = false;
            return pressed;
        }
        set;
    }

    public static void ClearKeyActions()
    {
        BackspacePressed = false;
        DeletePressed = false;
    }

    public static string GetInput()
    {
        if (Chars.Count <= 0)
        {
            return string.Empty;
        }

        var str = new string(Chars.ToArray());
        Chars.Clear();
        return str;
    }
}
