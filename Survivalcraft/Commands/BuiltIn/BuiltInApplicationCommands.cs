using Engine.Input;

using Game.Application;
using Game.Commands.Http;

namespace Game.Commands.BuiltIn;

public static class BuiltInApplicationCommands
{
    public static void Register(IModCommands commands, ModId owner)
    {
        new BuiltInProcessCommands(
            new ProcessCommandServices(
                GameExitManager.RequestRestart,
                GameExitManager.RequestExit,
                GameExitManager.RequestInstanceSwitch,
                () => Localization.ClientLanguage.Catalog,
                language =>
                {
                    AppConfigManager.Values["Language"] = language;
                    ClientApplication.Content.PublishLanguage(
                        CurrentModRuntime.Value!.Content.CreateLanguageCatalog(language)
                    );
                },
                SettingsManager.SaveSettings
            ),
            NetworkDiagnosticCommandHandlers.Diagnostics
        ).Register(commands, owner);
        commands.Register(
            new ResourceId(owner, "automation/ui/context/get"),
            new CommandDefinition<GetAutomationUiContextCommand>(
                AutomationCommandHandlers.GetContext,
                CommandDomain.Application,
                CommandDescription("AutomationUiContext_Description", "显示自动化 UI 上下文")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/input/run"),
            new CommandDefinition<RunAutomationInputCommand>(
                AutomationCommandHandlers.RunInput,
                CommandDomain.Application,
                LocalizedText.Literal("执行有时限的组合输入")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/gameplay/context/get"),
            new CommandDefinition<GetAutomationGameplayContextCommand>(
                AutomationCommandHandlers.GameplayContext,
                CommandDomain.Application,
                LocalizedText.Literal("读取角色操作上下文")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/view/look_at"),
            new CommandDefinition<LookAtAutomationTargetCommand>(
                AutomationCommandHandlers.LookAt,
                CommandDomain.Application,
                LocalizedText.Literal("朝向世界坐标")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/view/status"),
            new CommandDefinition<GetAutomationViewStatusCommand>(
                AutomationCommandHandlers.ViewStatus,
                CommandDomain.Application,
                LocalizedText.Literal("读取视角控制状态")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/view/angles"),
            new CommandDefinition<SetAutomationViewAnglesCommand>(
                AutomationCommandHandlers.SetViewAngles,
                CommandDomain.Application,
                LocalizedText.Literal("按角度调整视角")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/view/cancel"),
            new CommandDefinition<CancelAutomationViewCommand>(
                AutomationCommandHandlers.CancelView,
                CommandDomain.Application,
                LocalizedText.Literal("取消视角控制")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/navigation/start"),
            new CommandDefinition<NavigateAutomationPlayerCommand>(
                AutomationCommandHandlers.Navigate,
                CommandDomain.Application,
                LocalizedText.Literal("自动步行到指定位置")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/navigation/status"),
            new CommandDefinition<GetAutomationNavigationStatusCommand>(
                AutomationCommandHandlers.NavigationStatus,
                CommandDomain.Application,
                LocalizedText.Literal("读取自动导航状态")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/navigation/cancel"),
            new CommandDefinition<CancelAutomationNavigationCommand>(
                AutomationCommandHandlers.CancelNavigation,
                CommandDomain.Application,
                LocalizedText.Literal("取消自动导航")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/input/status"),
            new CommandDefinition<GetAutomationActionStatusCommand>(
                AutomationCommandHandlers.ActionStatus,
                CommandDomain.Application,
                LocalizedText.Literal("读取自动化输入状态")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/input/cancel"),
            new CommandDefinition<CancelAutomationActionCommand>(
                AutomationCommandHandlers.CancelAction,
                CommandDomain.Application,
                LocalizedText.Literal("取消自动化输入")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/input/text"),
            new CommandDefinition<EnterAutomationTextCommand>(
                AutomationCommandHandlers.EnterText,
                CommandDomain.Application,
                LocalizedText.Literal("发送自动化文本输入")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/ui/tap"),
            new CommandDefinition<TapAutomationUiCommand>(
                AutomationCommandHandlers.Tap,
                CommandDomain.Application,
                CommandDescription("AutomationUiTap_Description", "点击自动化 UI 目标")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/ui/drag"),
            new CommandDefinition<DragAutomationUiCommand>(
                AutomationCommandHandlers.Drag,
                CommandDomain.Application,
                LocalizedText.Literal("拖动库存物品")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/ui/scroll"),
            new CommandDefinition<ScrollAutomationUiCommand>(
                AutomationCommandHandlers.Scroll,
                CommandDomain.Application,
                CommandDescription("AutomationUiScroll_Description", "滚动自动化 UI 目标")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/ui/swipe"),
            new CommandDefinition<SwipeAutomationUiCommand>(
                AutomationCommandHandlers.Swipe,
                CommandDomain.Application,
                CommandDescription("AutomationUiSwipe_Description", "滑动自动化 UI 目标")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/input/mouse/move"),
            new CommandDefinition<MoveAutomationMouseCommand>(
                AutomationCommandHandlers.MoveMouse,
                CommandDomain.Application,
                CommandDescription("AutomationMouseMove_Description", "发送相对鼠标移动")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/ui/key"),
            new CommandDefinition<PressAutomationKeyCommand>(
                AutomationCommandHandlers.PressKey,
                CommandDomain.Application,
                CommandDescription("AutomationUiKey_Description", "发送自动化 UI 按键")
            )
        );
        commands.Register(
            new ResourceId(owner, "automation/ui/screenshot"),
            new CommandDefinition<CaptureAutomationScreenshotCommand>(
                AutomationCommandHandlers.Screenshot,
                CommandDomain.Application,
                CommandDescription("AutomationUiScreenshot_Description", "捕获自动化 UI 截图")
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/ui/context/get"),
            HttpCommandBinding.Create(static _ => new GetAutomationUiContextCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/input/run"),
            HttpCommandBinding.Create(
                arguments => new RunAutomationInputCommand(
                    new Automation.AutomationInputAction(
                        arguments.GetOrDefault<string[]>("keys", []).Select(key => Enum.Parse<Key>(key, true))
                            .ToArray(),
                        arguments.GetOrDefault<string[]>("mouseButtons", [])
                            .Select(button => Enum.Parse<MouseButton>(button, true)).ToArray(),
                        arguments.GetOrDefault("durationFrames", 1),
                        arguments.GetOrDefault("mouseDeltaX", 0),
                        arguments.GetOrDefault("mouseDeltaY", 0),
                        arguments.GetOrDefault<double?>("durationSeconds", null)
                    )
                ),
                new HttpCommandArgumentDefinition("keys", "string[]", false),
                new HttpCommandArgumentDefinition("mouseButtons", "string[]", false),
                new HttpCommandArgumentDefinition("durationFrames", "integer", false),
                new HttpCommandArgumentDefinition("durationSeconds", "number", false),
                new HttpCommandArgumentDefinition("mouseDeltaX", "integer", false),
                new HttpCommandArgumentDefinition("mouseDeltaY", "integer", false)
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/input/status"),
            HttpCommandBinding.Create(static _ => new GetAutomationActionStatusCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/gameplay/context/get"),
            HttpCommandBinding.Create(static _ => new GetAutomationGameplayContextCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/view/look_at"),
            HttpCommandBinding.Create(
                arguments => new LookAtAutomationTargetCommand(
                    new Vector3(arguments.Get<float>("x"), arguments.Get<float>("y"), arguments.Get<float>("z")),
                    arguments.GetOrDefault("toleranceDegrees", 1f),
                    arguments.GetOrDefault("timeoutSeconds", 10d)
                ),
                new HttpCommandArgumentDefinition("x", "number"),
                new HttpCommandArgumentDefinition("y", "number"),
                new HttpCommandArgumentDefinition("z", "number"),
                new HttpCommandArgumentDefinition("toleranceDegrees", "number", false),
                new HttpCommandArgumentDefinition("timeoutSeconds", "number", false)
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/view/status"),
            HttpCommandBinding.Create(static _ => new GetAutomationViewStatusCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/view/angles"),
            HttpCommandBinding.Create(
                arguments => new SetAutomationViewAnglesCommand(
                    arguments.Get<float>("yawDegrees"),
                    arguments.Get<float>("pitchDegrees"),
                    arguments.GetOrDefault("relative", false),
                    arguments.GetOrDefault("toleranceDegrees", 1f),
                    arguments.GetOrDefault("timeoutSeconds", 10d)
                ),
                new HttpCommandArgumentDefinition("yawDegrees", "number"),
                new HttpCommandArgumentDefinition("pitchDegrees", "number"),
                new HttpCommandArgumentDefinition("relative", "boolean", false),
                new HttpCommandArgumentDefinition("toleranceDegrees", "number", false),
                new HttpCommandArgumentDefinition("timeoutSeconds", "number", false)
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/view/cancel"),
            HttpCommandBinding.Create(static _ => new CancelAutomationViewCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/navigation/start"),
            HttpCommandBinding.Create(
                arguments => new NavigateAutomationPlayerCommand(
                    new Vector3(arguments.Get<float>("x"), arguments.Get<float>("y"), arguments.Get<float>("z")),
                    arguments.GetOrDefault("range", 0.75f),
                    arguments.GetOrDefault("timeoutSeconds", 30d)
                ),
                new HttpCommandArgumentDefinition("x", "number"),
                new HttpCommandArgumentDefinition("y", "number"),
                new HttpCommandArgumentDefinition("z", "number"),
                new HttpCommandArgumentDefinition("range", "number", false),
                new HttpCommandArgumentDefinition("timeoutSeconds", "number", false)
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/navigation/status"),
            HttpCommandBinding.Create(static _ => new GetAutomationNavigationStatusCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/navigation/cancel"),
            HttpCommandBinding.Create(static _ => new CancelAutomationNavigationCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/input/cancel"),
            HttpCommandBinding.Create(static _ => new CancelAutomationActionCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/input/text"),
            HttpCommandBinding.Create(
                arguments => new EnterAutomationTextCommand(arguments.Get<string>("text")),
                new HttpCommandArgumentDefinition("text", "string")
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/ui/tap"),
            HttpCommandBinding.Create(
                arguments => new TapAutomationUiCommand(arguments.Get<string>("selector")),
                new HttpCommandArgumentDefinition("selector", "string")
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/ui/drag"),
            HttpCommandBinding.Create(
                arguments => new DragAutomationUiCommand(
                    arguments.Get<string>("sourceSelector"),
                    arguments.Get<string>("targetSelector"),
                    arguments.GetOrDefault("durationSeconds", 0.5d)
                ),
                new HttpCommandArgumentDefinition("sourceSelector", "string"),
                new HttpCommandArgumentDefinition("targetSelector", "string"),
                new HttpCommandArgumentDefinition("durationSeconds", "number", false)
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/ui/scroll"),
            HttpCommandBinding.Create(
                arguments => new ScrollAutomationUiCommand(
                    arguments.Get<string>("selector"),
                    arguments.Get<float>("delta")
                ),
                new HttpCommandArgumentDefinition("selector", "string"),
                new HttpCommandArgumentDefinition("delta", "number")
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/ui/swipe"),
            HttpCommandBinding.Create(
                arguments => new SwipeAutomationUiCommand(
                    arguments.Get<string>("selector"),
                    arguments.Get<float>("deltaX"),
                    arguments.Get<float>("deltaY"),
                    arguments.GetOrDefault("durationFrames", 4)
                ),
                new HttpCommandArgumentDefinition("selector", "string"),
                new HttpCommandArgumentDefinition("deltaX", "number"),
                new HttpCommandArgumentDefinition("deltaY", "number"),
                new HttpCommandArgumentDefinition("durationFrames", "integer", false)
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/input/mouse/move"),
            HttpCommandBinding.Create(
                arguments => new MoveAutomationMouseCommand(
                    arguments.Get<int>("deltaX"),
                    arguments.Get<int>("deltaY")
                ),
                new HttpCommandArgumentDefinition("deltaX", "integer"),
                new HttpCommandArgumentDefinition("deltaY", "integer")
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/ui/key"),
            HttpCommandBinding.Create(
                arguments => new PressAutomationKeyCommand(Enum.Parse<Key>(arguments.Get<string>("key"), true)),
                new HttpCommandArgumentDefinition("key", "string")
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "automation/ui/screenshot"),
            HttpCommandBinding.Create(static _ => new CaptureAutomationScreenshotCommand())
        );
    }

    private static LocalizedText CommandDescription(
        string key,
        string fallback
    )
    {
        return new LocalizedText("Commands", key, fallback);
    }
}
