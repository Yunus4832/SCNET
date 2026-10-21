using System.Xml.Linq;

using Engine.Core;
using Engine.FileStorage;
using Engine.Graphics;
using Engine.Media;
using Engine.Windowing;

using Game.Managers;
using Game.Modding;
using Game.Widgets;

namespace Scnet.UiPreviewTool;

internal static class PreviewRenderer
{
    public static int Run(PreviewOptions options, IUiPreviewScenario scenario)
    {
        Exception? failure = null;
        GameModRuntime? runtime = null;
        var completed = false;
        var scenarioDisposed = false;

        void DisposeScenario()
        {
            if (!scenarioDisposed)
            {
                scenarioDisposed = true;
                (scenario as IDisposable)?.Dispose();
            }
        }

        var errors = 0;
        var warnings = 0;
        var manifest = new XElement(
            "UiPreview",
            new XAttribute("Version", Program.Version),
            new XAttribute("Width", options.Width),
            new XAttribute("Height", options.Height),
            new XAttribute("Scale", options.Scale),
            new XAttribute("Source", options.XmlPath ?? options.FactoryType!)
        );
        Window.UnhandledException += info =>
        {
            failure = info.Exception;
            Window.Close();
        };
        Window.Created += () =>
        {
            try
            {
                Storage.RegisterFileSystemRoot("app", AppContext.BaseDirectory, readOnly: true);
                ContentManager.Initialize();
                runtime = GameModRuntime.Start(scenario.CreateMods());
                foreach (var culture in options.Cultures)
                {
                    if (!runtime.Content.LanguageTypes.Contains(culture, StringComparer.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException($"Unsupported built-in language: {culture}");
                    }
                }
            }
            catch (Exception exception)
            {
                failure = exception;
                Window.Close();
            }
        };
        Window.Frame += () =>
        {
            if (completed || failure != null)
            {
                Window.Close();
                return;
            }

            completed = true;
            try
            {
                foreach (var culture in options.Cultures)
                {
                    runtime!.InitializeLanguage(culture);
                    TextureAtlasManager.Initialize();
                    var logicalSize = new Vector2(options.Width / options.Scale, options.Height / options.Scale);
                    using var root = new CanvasWidget
                    {
                        Name = "Preview.Viewport",
                        Size = logicalSize,
                        LayoutTransform = Matrix.CreateScale(options.Scale),
                        ClampToBounds = true
                    };
                    var widget = scenario.Create(new UiPreviewContext(culture, logicalSize));
                    widget.HorizontalAlignment = WidgetAlignment.Center;
                    widget.VerticalAlignment = WidgetAlignment.Center;
                    root.Children.Add(widget);
                    var viewport = new Vector2(options.Width, options.Height);
                    PreviewLayout.Arrange(root, logicalSize);
                    var report = PreviewLayout.Describe(root, viewport, out var sceneErrors, out var sceneWarnings);
                    errors += sceneErrors;
                    warnings += sceneWarnings;
                    var imageName = culture + ".png";
                    var reportName = culture + ".xml";
                    if (sceneErrors == 0)
                    {
                        Capture(root, options, Path.Combine(options.OutputDirectory, imageName));
                    }

                    report.Save(Path.Combine(options.OutputDirectory, reportName));
                    manifest.Add(
                        new XElement(
                            "Scene",
                            new XAttribute("Culture", culture),
                            new XAttribute("Errors", sceneErrors),
                            new XAttribute("Warnings", sceneWarnings),
                            new XAttribute("Image", sceneErrors == 0 ? imageName : string.Empty),
                            new XAttribute("Layout", reportName)
                        )
                    );
                    Console.WriteLine($"{culture}: errors={sceneErrors}, warnings={sceneWarnings}; {reportName}");
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                Window.Close();
            }
        };
        Window.Closed += () =>
        {
            try
            {
                DisposeScenario();
                runtime?.Dispose();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        };
        try
        {
            Window.Run(320, 240, WindowMode.Fixed, "SCNET UI preview");
            if (!completed && failure == null)
            {
                failure = new InvalidOperationException("Window closed before previews were rendered.");
            }
        }
        catch (Exception exception)
        {
            failure ??= exception;
        }
        finally
        {
            DisposeScenario();
            if (failure != null)
            {
                manifest.Add(new XElement("Failure", failure.ToString()));
            }

            manifest.Save(Path.Combine(options.OutputDirectory, "preview.xml"));
        }

        if (failure != null)
        {
            throw new InvalidOperationException("UI preview failed; see preview.xml.", failure);
        }

        if (!completed)
        {
            throw new InvalidOperationException("Window closed before previews were rendered.");
        }

        return errors > 0 || options.Strict && warnings > 0 ? 3 : 0;
    }

    private static void Capture(Widget root, PreviewOptions options, string path)
    {
        using var target = new RenderTarget2D(
            options.Width,
            options.Height,
            1,
            ColorFormat.Rgba8888,
            DepthFormat.Depth24Stencil8
        );
        var previous = Display.RenderTarget;
        try
        {
            Display.RenderTarget = target;
            Display.Clear(new Color(35, 45, 55), 1f, 0);
            Widget.DrawWidgetsHierarchy(root);
            using var output = File.Create(path);
            RenderTarget2D.Save(target, output, ImageFileFormat.Png, false);
        }
        finally
        {
            Display.RenderTarget = previous;
        }
    }
}
