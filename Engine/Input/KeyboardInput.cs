namespace Engine.Input;

public class KeyboardInput
{
    public static readonly List<char> Chars = [];

    public static bool DeletePressed
    {
        get
        {
            var d = field;
            if (d)
            {
                field = false;
            }

            return d;
        }
        set;
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
