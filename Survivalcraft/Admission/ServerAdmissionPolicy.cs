using EntitySystem.TemplatesDatabase;

using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;

namespace Game.Admission;

/// <summary>Private world admission policy; only its public challenge is sent to clients.</summary>
public sealed class ServerAdmissionPolicy
{
    private readonly ServerPasswordVerifier? _password;

    private ServerAdmissionPolicy(
        ServerAdmissionMode mode,
        ServerPasswordVerifier? password = null,
        AuthenticationServiceEndpoint? authority = null
    )
    {
        Mode = mode;
        _password = password;
        Authority = authority;
    }

    public ServerAdmissionMode Mode { get; }

    public AuthenticationServiceEndpoint? Authority { get; }

    public static ServerAdmissionPolicy Open { get; } = new(ServerAdmissionMode.Open);

    public static ServerAdmissionPolicy WithPassword(string password)
    {
        return new(ServerAdmissionMode.Password, ServerPasswordVerifier.Create(password));
    }

    public static ServerAdmissionPolicy WithAuthority(AuthenticationServiceEndpoint authority)
    {
        return new(ServerAdmissionMode.External, authority: authority);
    }

    internal bool VerifyPassword(string password) => _password?.Verify(password) == true;

    public ValuesDictionary Save()
    {
        var data = new ValuesDictionary();
        data.SetValue("Mode", (int)Mode);
        if (_password is { } password)
        {
            data.SetValue("PasswordSalt", password.Salt);
            data.SetValue("PasswordVerifier", password.Hash);
        }

        if (Authority is { } authority)
        {
            data.SetValue("AuthorityName", authority.Description.DisplayName);
            data.SetValue("AuthorityUrl", authority.Description.ServiceUrl);
            data.SetValue("AuthorityPublicKey", authority.Description.PublicKey);
        }

        return data;
    }

    public static ServerAdmissionPolicy Load(ValuesDictionary data)
    {
        if (data.Count == 0) { return Open; }

        var mode = (ServerAdmissionMode)data.GetValue<int>("Mode");
        return mode switch
        {
            ServerAdmissionMode.Open when data.Count == 1 => Open,
            ServerAdmissionMode.Password when data.Count == 3 => new ServerAdmissionPolicy(
                mode,
                new ServerPasswordVerifier(
                    data.GetValue<string>("PasswordSalt"),
                    data.GetValue<string>("PasswordVerifier")
                )
            ),
            ServerAdmissionMode.External when data.Count == 4 => WithAuthority(
                new AuthenticationServiceEndpoint(
                    new AuthenticationServiceDescription(
                        SCNET.Authentication.Protocol.AuthenticationProtocol.Version,
                        data.GetValue<string>("AuthorityName"),
                        data.GetValue<string>("AuthorityUrl"),
                        data.GetValue<string>("AuthorityPublicKey")
                    )
                )
            ),
            _ => throw new InvalidDataException("Invalid server admission configuration; refusing to downgrade.")
        };
    }

    internal string Fingerprint()
    {
        var node = new System.Xml.Linq.XElement("Admission");
        Save().Save(node);
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(node.ToString(System.Xml.Linq.SaveOptions.DisableFormatting))
            )
        );
    }
}
