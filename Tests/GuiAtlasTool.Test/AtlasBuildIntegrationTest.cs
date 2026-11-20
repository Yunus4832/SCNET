using System.Diagnostics;
using System.Xml.Linq;

namespace GuiAtlasTool.Test;

public sealed class AtlasBuildIntegrationTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplicationGlobalPropertiesDoNotRetargetOrDisableTheHostBuild(bool noBuild)
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository != null && !File.Exists(Path.Combine(repository.FullName, "SCNET.slnx")))
        {
            repository = repository.Parent;
        }

        Assert.NotNull(repository);
        var temporary = Path.Combine(Path.GetTempPath(), "scnet-atlas-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var texture = Path.Combine(temporary, "atlas.webp");
            var definition = Path.Combine(temporary, "atlas.txt");
            var project = Path.Combine(temporary, "HostTool.proj");
            new XDocument(
                new XElement(
                    "Project",
                    new XElement(
                        "Import",
                        new XAttribute("Project", Path.Combine(repository.FullName, "Directory.Build.props"))
                    ),
                    new XElement(
                        "Import",
                        new XAttribute("Project", Path.Combine(repository.FullName, "Directory.Build.targets"))
                    ),
                    new XElement(
                        "PropertyGroup",
                        new XElement("UsePackResourceTarget", "true"),
                        new XElement("_GuiAtlasTexture", texture),
                        new XElement("_GuiAtlasDefinition", definition)
                    )
                )
            ).Save(project);
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = repository.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var argument in new[]
                     {
                         "msbuild", project, "-t:GenerateGuiAtlas", "-v:minimal", "-p:Configuration=Debug",
                         "-p:TargetFramework=net10.0-android36.0", "-p:RuntimeIdentifier=android-arm",
                         "-p:TargetPlatformIdentifier=android", "-p:TargetPlatformVersion=36.0"
                     })
            {
                start.ArgumentList.Add(argument);
            }

            start.ArgumentList.Add($"-p:NoBuild={noBuild}");

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(true);
                await process.WaitForExitAsync(timeout.Token);
                throw;
            }

            Assert.True(process.ExitCode == 0, await output + await error);
            Assert.True(File.Exists(texture));
            Assert.NotEmpty(await File.ReadAllLinesAsync(definition, timeout.Token));
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
    }
}
