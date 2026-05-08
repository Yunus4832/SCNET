namespace Engine.Graphics;

public class ShaderMacro
{
    private const string _nameChars1 = "_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private const string _nameChars2 = "_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public readonly string Name;

    public readonly string Value;

    public ShaderMacro(string name)
        : this(name, string.Empty)
    {
    }

    public ShaderMacro(string name, string value)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (name.Where((t, i) => (i == 0 && !_nameChars1.Contains(t)) || (i > 0 && !_nameChars2.Contains(t))).Any())
        {
            throw new ArgumentException("Invalid shader macro name.");
        }

        if (value.Contains('\n') ||
            (value.Length > 0 &&
             (char.IsWhiteSpace(value[0]) ||
              char.IsWhiteSpace(value[^1]))))
        {
            throw new ArgumentException("Invalid shader macro value.");
        }

        Name = name;
        Value = value;
    }

    public override bool Equals(object? obj)
    {
        return obj is ShaderMacro shaderMacro && Equals(shaderMacro);
    }

    private bool Equals(ShaderMacro shaderMacro)
    {
        return shaderMacro.Name == Name && shaderMacro.Value == Value;
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode() + Value.GetHashCode();
    }
}
