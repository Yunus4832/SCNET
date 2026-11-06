using Game.Commands.Http;
using Game.Commands.Text;

namespace Game.Commands.BuiltIn;

public sealed class BuiltInProcessCommands(ProcessCommandServices services, NetworkCommandDiagnostics diagnostics)
{
    private readonly ProcessCommandServices _services = services;

    private readonly NetworkCommandDiagnostics _diagnostics = diagnostics;

    public void Register(IModCommands commands, ModId owner)
    {
        commands.Register(
            new ResourceId(owner, "application/restart"),
            new CommandDefinition<RestartApplicationCommand>(
                ExecuteApplicationRestart,
                CommandDomain.Application,
                CommandDescription("ApplicationRestart_Description", "重新启动应用")
            )
        );
        commands.Register(
            new ResourceId(owner, "application/instance/switch"),
            new CommandDefinition<SwitchInstanceCommand>(
                ExecuteInstanceSwitch,
                CommandDomain.Application,
                CommandDescription("InstanceSwitch_Description", "切换数据实例并重启")
            )
        );
        commands.Register(
            new ResourceId(owner, "application/instance/create"),
            new CommandDefinition<CreateInstanceCommand>(
                ExecuteInstanceCreate,
                CommandDomain.Application,
                CommandDescription("InstanceCreate_Description", "创建数据实例")
            )
        );
        commands.Register(
            new ResourceId(owner, "application/instance/delete"),
            new CommandDefinition<DeleteInstanceCommand>(
                ExecuteInstanceDelete,
                CommandDomain.Application,
                CommandDescription("InstanceDelete_Description", "删除数据实例")
            )
        );
        commands.Register(
            new ResourceId(owner, "application/instance/clone"),
            new CommandDefinition<CloneInstanceCommand>(
                ExecuteInstanceClone,
                CommandDomain.Application,
                CommandDescription("InstanceClone_Description", "克隆数据实例")
            )
        );
        commands.Register(
            new ResourceId(owner, "application/exit"),
            new CommandDefinition<ExitApplicationCommand>(
                ExecuteApplicationExit,
                CommandDomain.Application,
                CommandDescription("ApplicationExit_Description", "退出应用")
            )
        );
        commands.Register(
            new ResourceId(owner, "application/language/get"),
            new CommandDefinition<GetLanguageCommand>(
                ExecuteLanguageGet,
                CommandDomain.Application,
                CommandDescription("LanguageGet_Description", "显示当前语言")
            )
        );
        commands.Register(
            new ResourceId(owner, "application/language/set"),
            new CommandDefinition<SetLanguageCommand>(
                ExecuteLanguageSet,
                CommandDomain.Application,
                CommandDescription("LanguageSet_Description", "切换本地界面语言")
            )
        );
        commands.Register(
            new ResourceId(owner, "diagnostics/network/get"),
            new CommandDefinition<GetNetworkSendStatisticsCommand>(
                _diagnostics.GetSendStatistics,
                CommandDomain.Application,
                CommandDescription("NetworkSendStatistics_Description", "读取连接内网络发送统计"),
                allowedPrincipals: CommandPrincipalKinds.LocalOperator
            )
        );
        commands.Adapters.Register(new ResourceId(owner, "text/language"), CreateLanguageText());

        commands.Adapters.Register(
            new ResourceId(owner, "application/language/get"),
            HttpCommandBinding.Create(static _ => new GetLanguageCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "diagnostics/network/get"),
            HttpCommandBinding.Create(static _ => new GetNetworkSendStatisticsCommand())
        );
    }


    public TextCommand CreateLanguageText()
    {
        return new TextCommand(
            "language",
            CommandDescription("Language_Description", "查询或切换本地界面语言"),
            [
                new CommandRoute(
                    [],
                    typeof(GetLanguageCommand),
                    _ => new GetLanguageCommand(),
                    CommandDescription("LanguageGet_Description", "显示当前本地语言")
                ),
                new CommandRoute(
                    [
                        new CommandArgument(
                            "language",
                            SuggestionProvider: SuggestLanguages
                        )
                    ],
                    typeof(SetLanguageCommand),
                    arguments => new SetLanguageCommand(arguments.Get<string>("language")),
                    CommandDescription("LanguageSet_Description", "切换本地界面语言")
                )
            ],
            ["lang"]
        );
    }


    private CommandResult ExecuteApplicationRestart(
        CommandContext context,
        RestartApplicationCommand command
    )
    {
        _services.Restart(command.RestartSession);
        return CommandResult.LocalizedOk(
            "application.restarting",
            "ApplicationRestarting_Message",
            "正在重新启动应用。"
        );
    }

    private CommandResult ExecuteInstanceSwitch(
        CommandContext context,
        SwitchInstanceCommand command
    )
    {
        var instanceId = command.InstanceId?.Trim() ?? string.Empty;
        if (string.Equals(
                instanceId,
                StarterInstanceManager.Current.Id,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return CommandResult.LocalizedFail(
                "application.instance.unchanged",
                "InstanceUnchanged_Message",
                "当前已经在该实例中运行。"
            );
        }

        if (!StarterInstanceManager.ListInstances().Contains(
                instanceId,
                StringComparer.OrdinalIgnoreCase
            ))
        {
            return CommandResult.LocalizedFail(
                "application.instance.not_found",
                "InstanceNotFound_Message",
                "找不到实例：{0}。",
                instanceId
            );
        }

        _services.SwitchInstance(instanceId);
        return CommandResult.LocalizedOk(
            "application.instance.switching",
            "InstanceSwitching_Message",
            "正在切换到实例 {0}。",
            instanceId
        );
    }

    private CommandResult ExecuteApplicationExit(
        CommandContext context,
        ExitApplicationCommand command
    )
    {
        _services.Exit();
        return CommandResult.LocalizedOk(
            "application.exiting",
            "ApplicationExiting_Message",
            "正在退出应用。"
        );
    }

    private CommandResult ExecuteInstanceCreate(
        CommandContext context,
        CreateInstanceCommand command
    )
    {
        var instanceId = command.InstanceId?.Trim() ?? string.Empty;
        try
        {
            StarterInstanceManager.CreateInstance(instanceId);
        }
        catch (ArgumentException)
        {
            return CommandResult.LocalizedFail(
                "application.instance.invalid_id",
                "InstanceInvalidId_Message",
                "实例 ID 只能包含英文字母、数字、'-' 和 '_'。"
            );
        }
        catch (InvalidOperationException)
        {
            return CommandResult.LocalizedFail(
                "application.instance.already_exists",
                "InstanceAlreadyExists_Message",
                "实例已存在：{0}。",
                instanceId
            );
        }

        return CommandResult.LocalizedOk(
            "application.instance.created",
            "InstanceCreated_Message",
            "已创建实例 {0}。",
            instanceId
        );
    }

    private CommandResult ExecuteInstanceDelete(
        CommandContext context,
        DeleteInstanceCommand command
    )
    {
        var instanceId = command.InstanceId?.Trim() ?? string.Empty;
        if (string.Equals(instanceId, StarterInstanceManager.Current.Id, StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.LocalizedFail(
                "application.instance.current_delete_forbidden",
                "InstanceCurrentDeleteForbidden_Message",
                "不能删除当前实例。"
            );
        }

        if (!StarterInstanceManager.ListInstances().Contains(instanceId, StringComparer.OrdinalIgnoreCase))
        {
            return CommandResult.LocalizedFail(
                "application.instance.not_found",
                "InstanceNotFound_Message",
                "找不到实例：{0}。",
                instanceId
            );
        }

        if (StarterInstanceManager.IsInstanceRunning(instanceId))
        {
            return CommandResult.LocalizedFail(
                "application.instance.running_delete_forbidden",
                "InstanceRunningDeleteForbidden_Message",
                "不能删除正在运行的实例：{0}。",
                instanceId
            );
        }

        StarterInstanceManager.DeleteInstance(instanceId);
        return CommandResult.LocalizedOk(
            "application.instance.deleted",
            "InstanceDeleted_Message",
            "已删除实例 {0}。",
            instanceId
        );
    }

    private CommandResult ExecuteInstanceClone(
        CommandContext context,
        CloneInstanceCommand command
    )
    {
        var sourceInstanceId = command.SourceInstanceId?.Trim() ?? string.Empty;
        var targetInstanceId = command.TargetInstanceId?.Trim() ?? string.Empty;
        try
        {
            StarterInstanceManager.ValidateInstanceId(targetInstanceId);
        }
        catch (ArgumentException)
        {
            return CommandResult.LocalizedFail(
                "application.instance.invalid_id",
                "InstanceInvalidId_Message",
                "实例 ID 只能包含英文字母、数字、'-' 和 '_'。"
            );
        }

        if (!StarterInstanceManager.ListInstances().Contains(sourceInstanceId, StringComparer.OrdinalIgnoreCase))
        {
            return CommandResult.LocalizedFail(
                "application.instance.not_found",
                "InstanceNotFound_Message",
                "找不到实例：{0}。",
                sourceInstanceId
            );
        }

        if (!StarterInstanceManager.CanCloneInstance(sourceInstanceId))
        {
            return CommandResult.LocalizedFail(
                "application.instance.running_clone_forbidden",
                "InstanceRunningCloneForbidden_Message",
                "不能克隆其他进程正在使用的实例：{0}。",
                sourceInstanceId
            );
        }

        if (StarterInstanceManager.ListInstances().Contains(targetInstanceId, StringComparer.OrdinalIgnoreCase))
        {
            return CommandResult.LocalizedFail(
                "application.instance.already_exists",
                "InstanceAlreadyExists_Message",
                "实例已存在：{0}。",
                targetInstanceId
            );
        }

        try
        {
            StarterInstanceManager.CloneInstance(sourceInstanceId, targetInstanceId);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to clone starter instance '{sourceInstanceId}' to '{targetInstanceId}': {ex}");
            return CommandResult.LocalizedFail(
                "application.instance.clone_failed",
                "InstanceCloneFailed_Message",
                "克隆实例失败。"
            );
        }

        return CommandResult.LocalizedOk(
            "application.instance.cloned",
            "InstanceCloned_Message",
            "已将实例 {0} 克隆为 {1}。",
            sourceInstanceId,
            targetInstanceId
        );
    }

    private CommandResult ExecuteLanguageGet(
        CommandContext context,
        GetLanguageCommand command
    )
    {
        var languageType = CurrentLanguageType();
        return CommandResult.LocalizedOk(
            "application.language",
            "CurrentLanguage_Message",
            "当前语言：{0}（{1}）。",
            languageType,
            _services.GetLanguage().GetLanguageDisplayName(languageType)
        );
    }

    private CommandResult ExecuteLanguageSet(
        CommandContext context,
        SetLanguageCommand command
    )
    {
        var languageType = _services.GetLanguage().LanguageTypes.FirstOrDefault(type =>
            type.Equals(command.LanguageType, StringComparison.OrdinalIgnoreCase)
        );
        if (languageType is null)
        {
            return CommandResult.LocalizedFail(
                "application.language.invalid",
                "LanguageInvalid_Message",
                "不支持的语言：{0}。可用语言：{1}。",
                command.LanguageType,
                string.Join(", ", _services.GetLanguage().LanguageTypes)
            );
        }

        var currentLanguage = CurrentLanguageType();
        if (languageType.Equals(currentLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.LocalizedOk(
                "application.language.unchanged",
                "LanguageUnchanged_Message",
                "当前已经使用 {0}，无需切换。",
                _services.GetLanguage().GetLanguageDisplayName(languageType)
            );
        }

        _services.SetLanguage(languageType);
        _services.SaveSettings();
        return CommandResult.LocalizedOk(
            "application.language.changed",
            "LanguageChanged_Message",
            "语言已切换为 {0}（{1}）。",
            languageType,
            _services.GetLanguage().GetLanguageDisplayName(languageType)
        );
    }

    private string CurrentLanguageType()
    {
        return _services.GetLanguage().CurrentLanguage;
    }


    private IEnumerable<CommandArgumentSuggestion> SuggestLanguages(CommandSuggestionContext context)
    {
        return _services.GetLanguage().LanguageTypes.Select(languageType =>
            new CommandArgumentSuggestion(
                languageType,
                LocalizedText.Literal(_services.GetLanguage().GetLanguageDisplayName(languageType))
            )
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
