using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Game.Protocol.Generators.Test;

public sealed class MessageOwnershipGeneratorTest
{
    [Theory]
    [InlineData("[MessagePack.Key(0)] public byte[] Data { get; init; } = [];", true)]
    [InlineData("[MessagePack.Key(0)] public int Number { get; set; }", true)]
    [InlineData("[MessagePack.Key(0)] public int Number { get; init; }", false)]
    public void ContractChangesAutomaticallyChangeTheTypedCopyPlan(string member, bool mustCopy)
    {
        var (driver, output) = Generate(member);
        Assert.Empty(driver.Diagnostics.Where(value => value.Severity == DiagnosticSeverity.Error));
        Assert.Empty(output.GetDiagnostics().Where(value => value.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(Assert.Single(driver.Results).GeneratedSources).SourceText.ToString();
        Assert.Equal(mustCopy, generated.Contains("value with", StringComparison.Ordinal));
        if (member.Contains("byte[]", StringComparison.Ordinal)) { Assert.Contains("value.Data.ToArray()", generated); }
    }

    [Theory]
    [InlineData("[MessagePack.Key(0)] public byte[] Data { get; } = [];")]
    [InlineData("[MessagePack.Key(0)] public System.IO.Stream Data { get; init; } = null!;")]
    [InlineData("[MessagePack.Key(0)] public byte[] Data = [];")]
    [InlineData(
        "[MessagePack.Key(0)] public System.Collections.Generic.IReadOnlyList<Sample> Children { get; init; } = [];"
    )]
    public void UnsupportedMutableMembersFailGenerationRatherThanBeingShared(string member)
    {
        var (driver, _) = Generate(member);
        Assert.Contains(
            driver.Diagnostics,
            value => value.Id == "SCNETOWN001" && value.Severity == DiagnosticSeverity.Error
        );
    }

    private static (GeneratorDriverRunResult Driver, Compilation Output) Generate(string member)
    {
        var source = """
                     #nullable enable
                     namespace MessagePack
                     {
                         [System.AttributeUsage(System.AttributeTargets.Interface | System.AttributeTargets.Class, AllowMultiple=true)]
                         public sealed class UnionAttribute(int tag, System.Type type) : System.Attribute { }
                         public sealed class MessagePackObjectAttribute : System.Attribute { }
                         public sealed class KeyAttribute(int key) : System.Attribute { }
                     }
                     namespace Game.Protocol
                     {
                         [MessagePack.Union(0, typeof(Sample))] public interface IProtocolMessage { }
                         [MessagePack.MessagePackObject] public sealed record Sample : IProtocolMessage
                         {
                     """ +
                     member +
                     "\n }\n }";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "OwnershipFixture",
            [CSharpSyntaxTree.ParseText(source, parse)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new MessageOwnershipGenerator().AsSourceGenerator()],
            parseOptions: parse
        );
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult(), output);
    }
}
