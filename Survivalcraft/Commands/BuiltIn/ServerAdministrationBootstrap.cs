using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Enums;

namespace Game.Commands;

public static class ServerAdministrationBootstrap
{
    private const string _codeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static readonly ConditionalWeakTable<Project, BootstrapState> _states = new();

    public static bool IsClaimed(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var state = GetState(project);
        lock (state.SyncRoot)
        {
            return EnsureClaimedState(project, state);
        }
    }

    public static bool TryGetClaimCode(Project project, out string code)
    {
        ArgumentNullException.ThrowIfNull(project);
        var state = GetState(project);
        lock (state.SyncRoot)
        {
            if (CommonLib.WorkType is not WorkType.Server ||
                EnsureClaimedState(project, state))
            {
                code = string.Empty;
                return false;
            }

            state.ClaimCode ??= GenerateClaimCode();
            code = state.ClaimCode;
            return true;
        }
    }

    public static bool TryRegenerateClaimCode(Project project, out string code)
    {
        ArgumentNullException.ThrowIfNull(project);
        var state = GetState(project);
        lock (state.SyncRoot)
        {
            if (CommonLib.WorkType is not WorkType.Server ||
                EnsureClaimedState(project, state))
            {
                code = string.Empty;
                return false;
            }

            state.ClaimCode = GenerateClaimCode();
            code = state.ClaimCode;
            return true;
        }
    }

    public static BootstrapClaimResult TryClaim(
        Project project,
        PlayerData player,
        string claimCode)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(player);
        if (CommonLib.WorkType is not WorkType.Server)
        {
            return BootstrapClaimResult.LocalizedFail(
                "auth.server_only",
                "AuthServerOnly_Message",
                "服务器认领只能在服务器上完成。");
        }

        if (!ReferenceEquals(player.Project, project) ||
            player.Client is not { State: not ClientState.NotConnected })
        {
            return BootstrapClaimResult.LocalizedFail(
                "auth.player_offline",
                "AuthPlayerBindingInvalid_Message",
                "认领必须绑定到当前服务器中的在线玩家。");
        }

        var state = GetState(project);
        lock (state.SyncRoot)
        {
            if (EnsureClaimedState(project, state))
            {
                return BootstrapClaimResult.LocalizedFail(
                    "auth.already_claimed",
                    "AuthClaimed_Message",
                    "服务器管理员已经完成首次认领。");
            }

            state.ClaimCode ??= GenerateClaimCode();
            if (!CodesEqual(state.ClaimCode, claimCode))
            {
                return BootstrapClaimResult.LocalizedFail(
                    "auth.invalid_code",
                    "AuthInvalidCode_Message",
                    "服务器认领码不正确。");
            }

            player.CommandPermissions.Grant(
                BuiltInPermissionIds.ManageStandard,
                canDelegate: true);
            project.FindSubsystem<SubsystemGameInfo>(true)!
                .ServerAdministrationClaimed = true;
            state.ClaimCode = null;
            return BootstrapClaimResult.LocalizedOk(
                "AuthClaimSuccess_Message",
                "玩家 {0} 已成为首位权限管理员，可管理和再授权标准指令权限。",
                player.Name);
        }
    }

    private static BootstrapState GetState(Project project)
    {
        return _states.GetValue(project, static _ => new BootstrapState());
    }

    private static bool EnsureClaimedState(Project project, BootstrapState state)
    {
        var gameInfo = project.FindSubsystem<SubsystemGameInfo>(true)!;
        var players = project.FindSubsystem<SubsystemPlayers>(true)!;
        var hasAdministrator = players.PlayersData.Any(player =>
            player.CommandPermissions.Grants.Any(grant =>
                grant.CanDelegate &&
                grant.Permission == BuiltInPermissionIds.ManageStandard));
        if (hasAdministrator)
        {
            gameInfo.ServerAdministrationClaimed = true;
            state.ClaimCode = null;
            return true;
        }

        gameInfo.ServerAdministrationClaimed = false;
        return false;
    }

    internal static string GenerateClaimCode()
    {
        Span<byte> bytes = stackalloc byte[12];
        RandomNumberGenerator.Fill(bytes);
        Span<char> characters = stackalloc char[14];
        var outputIndex = 0;
        for (var index = 0; index < bytes.Length; index++)
        {
            if (index is 4 or 8)
            {
                characters[outputIndex++] = '-';
            }

            characters[outputIndex++] = _codeAlphabet[bytes[index] & 31];
        }

        return new string(characters);
    }

    internal static bool CodesEqual(string expected, string actual)
    {
        var expectedBytes = Encoding.ASCII.GetBytes(NormalizeCode(expected));
        var actualBytes = Encoding.ASCII.GetBytes(NormalizeCode(actual));
        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string NormalizeCode(string code)
    {
        return string.Concat((code ?? string.Empty)
            .Where(character => character != '-')
            .Select(char.ToUpperInvariant));
    }

    private sealed class BootstrapState
    {
        public object SyncRoot { get; } = new();

        public string? ClaimCode { get; set; }
    }
}

public sealed record BootstrapClaimResult(
    bool Success,
    string Code,
    string Message,
    string MessageKey = "",
    IReadOnlyList<string>? MessageArguments = null)
{
    public static BootstrapClaimResult LocalizedOk(
        string messageKey,
        string fallback,
        params string[] arguments)
    {
        return new BootstrapClaimResult(
            true,
            "auth.claimed",
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                fallback,
                arguments.Cast<object>().ToArray()),
            messageKey,
            arguments);
    }

    public static BootstrapClaimResult LocalizedFail(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments)
    {
        return new BootstrapClaimResult(
            false,
            code,
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                fallback,
                arguments.Cast<object>().ToArray()),
            messageKey,
            arguments);
    }
}
