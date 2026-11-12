using System.Text;

namespace Game.Managers;

public static class ShaderCodeManager
{
    public static string GetFast(string fileName) => ExpandIncludes(ReadSource(fileName), ReadSource);

    internal static string ExpandIncludes(string source, Func<string, string> readSource)
    {
        return ExpandIncludes(source, readSource, new HashSet<string>(StringComparer.Ordinal));
    }

    internal static string ReadSource(string fileName)
    {
        var extension = fileName.LastIndexOf('.');
        if (extension <= 0) { throw new InvalidDataException($"Shader source has no extension: {fileName}"); }

        return ContentManager.Get<string>(fileName[..extension], fileName[extension..]);
    }

    private static string ExpandIncludes(string source, Func<string, string> readSource, HashSet<string> active)
    {
        var result = new StringBuilder();
        using var reader = new StringReader(source);
        while (reader.ReadLine() is { } line)
        {
            var directive = line.Trim();
            if (!directive.StartsWith("#include", StringComparison.Ordinal))
            {
                result.Append(line);
                result.Append('\n');
                continue;
            }

            var argument = directive[8..].Trim();
            if (argument.Length < 3 || argument[0] != '"' || argument[^1] != '"')
            {
                throw new InvalidDataException($"Invalid shader include: {line}");
            }

            var name = argument[1..^1];
            if (!active.Add(name)) { throw new InvalidDataException($"Cyclic shader include: {name}"); }

            try { result.Append(ExpandIncludes(readSource(name), readSource, active)); }
            finally { active.Remove(name); }
        }

        return result.ToString();
    }
}
