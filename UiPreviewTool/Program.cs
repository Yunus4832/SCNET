using System.Reflection;

namespace Scnet.UiPreviewTool;

internal static class Program
{
    internal static string Version =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+')[0];

    public static int Main(string[] args)
    {
        if (args is ["--help"] or ["-h"])
        {
            Console.WriteLine(
                """
                SCNET UI Preview
                --xml FILE | --assembly FILE --factory TYPE
                --output EMPTY_DIRECTORY
                [--culture zh-CN,en-US,pt-PT,ru-RU] [--size 960x640] [--scale 1] [--strict]

                Writes PNG images and XML layout/check reports without loading a world.
                Requires a working desktop graphics environment; it briefly opens a window.
                Exit codes: 0 success, 1 runtime failure, 2 invalid arguments, 3 layout check failure.
                """
            );
            return 0;
        }

        if (args is ["--version"])
        {
            Console.WriteLine(Version);
            return 0;
        }

        PreviewOptions options;
        IUiPreviewScenario scenario;
        try
        {
            options = PreviewOptions.Parse(args);
            if (Directory.Exists(options.OutputDirectory) &&
                Directory.EnumerateFileSystemEntries(options.OutputDirectory).Any())
            {
                throw new ArgumentException(
                    "Output directory must be empty; choose a new directory to preserve earlier results."
                );
            }

            scenario = PreviewScenarioLoader.Load(options);
            Directory.CreateDirectory(options.OutputDirectory);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }

        try
        {
            return PreviewRenderer.Run(options, scenario);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
