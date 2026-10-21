#:package Microsoft.CodeAnalysis.CSharp@5.0.0

using System.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet run --file check_explicit_enum_values.cs -- <files...> | --all");
    return 2;
}

IEnumerable<string> files = args;
if (args is ["--all"])
{
    var start = new ProcessStartInfo("git")
    {
        RedirectStandardOutput = true,
        UseShellExecute = false
    };
    foreach (var argument in new[] { "ls-files", "--cached", "--others", "--exclude-standard", "--", "*.cs" })
    {
        start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start git.");
    var output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        return process.ExitCode;
    }

    files = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

var violations = new HashSet<(string File, int Start)>();
var errors = 0;
var inspected = 0;
foreach (var file in files.Distinct(StringComparer.Ordinal))
{
    if (!File.Exists(file) || !file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    inspected++;
    var text = File.ReadAllText(file);
    if (!text.Contains("enum", StringComparison.Ordinal))
    {
        continue;
    }

    var options = new CSharpParseOptions(LanguageVersion.Preview);
    var initial = CSharpSyntaxTree.ParseText(text, options, file);
    var symbols = initial.GetRoot().DescendantTrivia(descendIntoTrivia: true)
        .Where(trivia => trivia.IsDirective)
        .Select(trivia => trivia.GetStructure())
        .SelectMany(node => node switch
            {
                IfDirectiveTriviaSyntax directive => directive.Condition.DescendantNodesAndSelf(),
                ElifDirectiveTriviaSyntax directive => directive.Condition.DescendantNodesAndSelf(),
                _ => Enumerable.Empty<SyntaxNode>()
            }
        )
        .OfType<IdentifierNameSyntax>()
        .Select(node => node.Identifier.ValueText)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    // Check every branch, including platform-specific enum members. Bound unusual inputs explicitly.
    if (symbols.Length > 10)
    {
        Console.Error.WriteLine(
            $"{file}: error SCNETSTYLE001: Too many conditional symbols for exhaustive enum checking."
        );
        errors++;
        continue;
    }

    for (var mask = 0; mask < 1 << symbols.Length; mask++)
    {
        var enabled = symbols.Where((_, index) => (mask & (1 << index)) != 0);
        var tree = mask == 0
            ? initial
            : CSharpSyntaxTree.ParseText(text, options.WithPreprocessorSymbols(enabled), file);
        foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>())
        {
            foreach (var member in declaration.Members)
            {
                if (member.EqualsValue is not null || !violations.Add((file, member.SpanStart)))
                {
                    continue;
                }

                var position = member.Identifier.GetLocation().GetLineSpan().StartLinePosition;
                Console.Error.WriteLine(
                    $"{file}({position.Line + 1},{position.Character + 1}): error SCNETSTYLE001: Enum member '{declaration.Identifier.ValueText}.{member.Identifier.ValueText}' requires an explicit value."
                );
            }
        }
    }
}

Console.WriteLine($"Enum style: {inspected} C# files, {violations.Count} implicit members, {errors} checking errors.");
return violations.Count == 0 && errors == 0 ? 0 : 1;
