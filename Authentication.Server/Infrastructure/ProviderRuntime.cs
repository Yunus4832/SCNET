using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.AspNetCore.Identity;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;
using SCNET.Authentication.Server.Domain;

namespace SCNET.Authentication.Server.Infrastructure;

/// <summary>Serializes aggregate access and commits durable provider grants with private atomic files.</summary>
public sealed class ProviderRuntime : IDisposable
{
    private readonly Lock _gate = new();

    private readonly ECDsa _identity;

    private readonly FileStream _lease;

    private readonly string _statePath;

    private readonly ProviderOptions _options;

    private readonly PasswordHasher<object> _passwords = new();

    private AuthenticationAuthority _authority;

    public ProviderRuntime(ProviderOptions options)
    {
        _options = options;
        var baseUri = AuthenticationServiceEndpoint.ParseBaseUri(options.PublicUrl);
        var directory = Path.GetFullPath(options.DataDirectory);
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var leaseOptions = new FileStreamOptions
        {
            Mode = FileMode.OpenOrCreate, Access = FileAccess.ReadWrite, Share = FileShare.None
        };
        if (!OperatingSystem.IsWindows())
        {
            leaseOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        _lease = new FileStream(Path.Combine(directory, ".lock"), leaseOptions);
        var keyPath = Path.Combine(directory, "identity.p8");
        _statePath = Path.Combine(directory, "authorizations.json");
        _identity = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            if (File.Exists(keyPath))
            {
                var bytes = File.ReadAllBytes(keyPath);
                try
                {
                    _identity.ImportPkcs8PrivateKey(bytes, out var consumed);
                    if (consumed != bytes.Length) { throw new CryptographicException("Invalid provider identity."); }
                }
                finally { CryptographicOperations.ZeroMemory(bytes); }
            }
            else
            {
                if (File.Exists(_statePath))
                {
                    throw new InvalidDataException(
                        "Provider identity is missing; refusing to replace an established identity."
                    );
                }

                var bytes = _identity.ExportPkcs8PrivateKey();
                try { WritePrivateFile(keyPath, bytes); }
                finally { CryptographicOperations.ZeroMemory(bytes); }
            }

            PublicKey = AuthenticationPublicKey.From(_identity);
            Service = new AuthenticationServiceEndpoint(
                new AuthenticationServiceDescription(
                    AuthenticationProtocol.Version,
                    options.DisplayName,
                    options.PublicUrl,
                    PublicKey.EncodedKey
                )
            );
            var state = File.Exists(_statePath)
                ? JsonSerializer.Deserialize<ProviderAuthorizationState>(File.ReadAllBytes(_statePath)) ??
                  throw new InvalidDataException("Invalid provider authorization state.")
                : new ProviderAuthorizationState();
            _authority = new AuthenticationAuthority(_identity, state, baseUri);
        }
        catch
        {
            _identity.Dispose();
            _lease.Dispose();
            throw;
        }
    }

    public AuthenticationPublicKey PublicKey { get; }

    public AuthenticationServiceEndpoint Service { get; }

    public AuthenticationReplayCache ReplayCache { get; } = new();

    public T Execute<T>(Func<AuthenticationAuthority, T> action, bool persist = false)
    {
        lock (_gate)
        {
            var previous = persist ? JsonSerializer.SerializeToUtf8Bytes(_authority.State) : null;
            try
            {
                var result = action(_authority);
                if (persist) { WritePrivateFile(_statePath, JsonSerializer.SerializeToUtf8Bytes(_authority.State)); }

                return result;
            }
            catch
            {
                if (persist)
                {
                    // Fail closed: discard outstanding transactions instead of acknowledging an uncommitted grant.
                    _authority = new AuthenticationAuthority(
                        _identity,
                        JsonSerializer.Deserialize<ProviderAuthorizationState>(previous!)!,
                        Service.BaseUri
                    );
                }

                throw;
            }
        }
    }

    public bool VerifyPassword(string account, string password)
    {
        return account.Length <= 128 &&
               password.Length <= 1024 &&
               _options.Accounts.TryGetValue(account, out var hash) &&
               _passwords.VerifyHashedPassword(this, hash, password) != PasswordVerificationResult.Failed;
    }

    public void Dispose()
    {
        _identity.Dispose();
        _lease.Dispose();
    }

    private static void WritePrivateFile(string path, ReadOnlySpan<byte> bytes)
    {
        var temporary = path + ".tmp";
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(temporary, options))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }

        File.Move(temporary, path, true);
    }
}
