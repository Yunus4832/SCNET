using System.Text;

namespace Game.Commands;

internal static class CommandLineTokenizer
{
    public static bool TryTokenize(string commandLine, out IReadOnlyList<string> tokens, out string error)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var escaped = false;

        foreach (var character in commandLine)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            switch (character)
            {
                case '\\':
                    escaped = true;
                    continue;
                case '"':
                    quoted = !quoted;
                    continue;
            }

            if (char.IsWhiteSpace(character) && !quoted)
            {
                Flush(result, current);
                continue;
            }

            current.Append(character);
        }

        if (escaped)
        {
            current.Append('\\');
        }

        if (quoted)
        {
            tokens = [];
            error = "指令中存在未闭合的引号。";
            return false;
        }

        Flush(result, current);
        tokens = result;
        error = string.Empty;
        return true;
    }

    public static IReadOnlyList<string> TokenizePartial(string commandLine)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var escaped = false;

        foreach (var character in commandLine)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (char.IsWhiteSpace(character) && !quoted)
            {
                Flush(result, current);
                continue;
            }

            current.Append(character);
        }

        result.Add(current.ToString());
        return result;
    }

    public static string FormatToken(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 0 &&
            !value.Any(character => char.IsWhiteSpace(character) || character is '"' or '\\'))
        {
            return value;
        }

        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    public static string ReplaceCurrentToken(string commandLine, string formattedValue)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(formattedValue);
        var tokenStart = 0;
        var quoted = false;
        var escaped = false;
        for (var index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (char.IsWhiteSpace(character) && !quoted)
            {
                tokenStart = index + 1;
            }
        }

        if (tokenStart == 0 && commandLine.StartsWith('/'))
        {
            tokenStart = 1;
        }

        return commandLine[..tokenStart] + formattedValue;
    }

    private static void Flush(ICollection<string> result, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        result.Add(current.ToString());
        current.Clear();
    }
}
