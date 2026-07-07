using System.Text.RegularExpressions;

namespace Game.Managers;

public class ShaderCodeManager
{
    public static string GetFast(string fileName)
    {
        var shaderText = string.Empty;
        var parameters = fileName.Split('.');
        if (parameters.Length > 1)
        {
            shaderText = ContentManager.Get<string>(parameters[0], "." + parameters[1]);
        }

        return shaderText;
    }

    public static string Get(string fileName)
    {
        var shaderText = string.Empty;
        shaderText = GetIncludeText(shaderText, fileName, false);
        return shaderText;
    }

    public static string GetExternal(string fileName)
    {
        var shaderText = string.Empty;
        shaderText = GetIncludeText(shaderText, fileName, true);
        return shaderText;
    }

    private static string GetIncludeText(string shaderText, string includedFileName, bool external)
    {
        var includeText = string.Empty;
        try
        {
            string shaderTextTemp;
            if (external)
            {
                var stream = Storage.OpenFile(Storage.CombinePaths(RunPath.ExternalPath, includedFileName),
                    OpenFileMode.Read);
                var streamReader = new StreamReader(stream);
                shaderTextTemp = streamReader.ReadToEnd();
            }
            else
            {
                if (includedFileName.Contains(".txt"))
                {
                    includedFileName = includedFileName.Split(['.'])[0];
                    shaderTextTemp = ContentManager.Get<string>(includedFileName);
                }
                else
                {
                    shaderTextTemp = GetFast(includedFileName);
                }
            }

            if (shaderTextTemp == string.Empty)
            {
                return string.Empty;
            }

            shaderTextTemp = shaderTextTemp.Replace("\n", "$");
            var lines = shaderTextTemp.Split(['$'], StringSplitOptions.RemoveEmptyEntries);
            for (var l = 0; l < lines.Length; l++)
            {
                lines[l] = lines[l].Trim();
                if (lines[l].StartsWith("//"))
                {
                    var text = lines[l][2..].TrimStart();
                    if (text.StartsWith('<') && text.EndsWith("/>"))
                    {
                        includeText += lines[l] + "\n";
                        continue;
                    }
                }

                var arline = lines[l].Replace("//", "$").Split(['$']);
                if (arline.Length > 0)
                {
                    lines[l] = arline[0];
                }

                if (lines[l].StartsWith("#include"))
                {
                    var regex = new Regex("\"[^\"]*\"");
                    var fileName = regex.Match(lines[l]).Value.Replace("\"", "");
                    includeText += GetIncludeText(shaderText, fileName, external);
                }
                else
                {
                    if (PlatformManager.Platform is Platform.Android)
                    {
                        includeText += lines[l] + "\n";
                    }

                    if (PlatformManager.Platform is Platform.Desktop)
                    {
                        includeText += lines[l].Replace("highp", "").Replace("lowp", "").Replace("mediump", "") + "\n";
                        includeText += lines[l] + "\n";
                    }
                }
            }

            shaderText += includeText;
        }
        catch
        {
            // ignored
        }

        return shaderText;
    }
}
