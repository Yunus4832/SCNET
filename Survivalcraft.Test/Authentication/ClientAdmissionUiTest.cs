using System.Reflection;
using System.Security.Cryptography;

using Game.Admission;
using Game.Authentication;
using Game.Managers;
using Game.Network;
using Game.Screens;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Authentication;

[Collection(ConfigFileCollection.Name)]
public sealed class ClientAdmissionUiTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RememberedPasswordIsCommittedOnlyAfterAcceptance(bool remember)
    {
        using var fixture = new Fixture();
        using var ui = fixture.CreateUi();
        var directory = Path.Combine(Path.GetTempPath(), "scnet-password-ui-" + Guid.NewGuid().ToString("N"));
        var current = typeof(AuthenticationManager).GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = current.GetValue(null);
        try
        {
            var store = new ServerCredentialStore(Path.Combine(directory, "credentials.xml"));
            current.SetValue(
                null,
                new AuthenticationManager(
                    new AuthenticationServiceRegistry(Path.Combine(directory, "services.xml")),
                    new AuthenticationGrantStore(Path.Combine(directory, "grants.xml")),
                    store
                )
            );
            var server = fixture.Node.CaptureAdmissionChallenge().Server;
            Set(ui, "_passwordServer", server);
            Set(ui, "_submittedPassword", "temporary test password");
            Set(ui, "_rememberPassword", remember);
            Assert.Null(store.Find(server));
            var nonce = fixture.Node.PendingAdmissionChallenge!.Nonce;
            typeof(ClientAdmissionUi).GetMethod("Receive", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
                ui,
                [new AdmissionResultPackage(nonce, AdmissionResultCode.Accepted)]
            );
            Assert.Equal(remember ? "temporary test password" : null, store.Find(server));
            Assert.Null(
                typeof(ClientAdmissionUi).GetField(
                    "_submittedPassword",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )!.GetValue(ui)
            );
        }
        finally
        {
            current.SetValue(null, previous);
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void ExplicitCancellationRemainsSilentEvenIfTheServerCancellationArrivesLate()
    {
        using var fixture = new Fixture();
        using var ui = fixture.CreateUi();
        typeof(ClientAdmissionUi).GetMethod("Cancel", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ui, null);
        Assert.Equal(string.Empty, fixture.Node.Error);
        var challenge = fixture.Node.PendingAdmissionChallenge!;
        typeof(NetNode).GetMethod("ApplyAdmissionResult", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
            fixture.Node,
            [new AdmissionResultPackage(challenge.Nonce, AdmissionResultCode.Cancelled)]
        );
        Assert.Equal(string.Empty, fixture.Node.Error);
    }

    [Fact]
    public async Task LoginOnceResultRemainsInConnectionMemoryAndStopsClearIt()
    {
        using var fixture = new Fixture();
        using var ui = fixture.CreateUi();
        Set(ui, "_authorization", Task.FromResult(fixture.Result));
        ui.Update();
        var context = Assert.IsType<TemporaryJoinAuthorization>(fixture.Node.TemporaryAuthentication);
        Assert.True(context.Grant!.Secret == fixture.Result.Grant!.Secret);
        Assert.Equal(fixture.Scope, context.Scope);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (!fixture.Node.AdmissionAccepted && DateTimeOffset.UtcNow < deadline)
        {
            fixture.Node.PumpLocalAdmission();
            await Task.Delay(5);
        }

        Assert.True(fixture.Node.AdmissionAccepted);
        fixture.Node.PrepareLocalHostWorld();
        Assert.Same(context, fixture.Node.TemporaryAuthentication);
        fixture.Node.Stop();
        Assert.Null(fixture.Node.TemporaryAuthentication);
    }

    [Fact]
    public void DisposedAdmissionCannotPublishALateTemporaryGrantOrSubmitItsProof()
    {
        using var fixture = new Fixture();
        var completed =
            new TaskCompletionSource<AuthenticationLoginResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ui = fixture.CreateUi();
        Set(ui, "_authorization", completed.Task);
        ui.Dispose();
        completed.SetResult(fixture.Result);
        ui.Update();
        Assert.Null(fixture.Node.TemporaryAuthentication);
        Assert.False(fixture.Node.AdmissionAccepted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BrowserResultAfterTransportLossOnlyReturnsOneScopedGrantUnlessCancelled(bool cancelled)
    {
        using var fixture = new Fixture();
        using var ui = fixture.CreateUi();
        var completed =
            new TaskCompletionSource<AuthenticationLoginResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Set(ui, "_authorization", completed.Task);
        Set(ui, "_browserOpened", true);
        Assert.True(
            (bool)typeof(ClientAdmissionUi)
                .GetMethod("PreserveExternalAuthentication", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(ui, null)!
        );
        fixture.Node.CurrentStage = NetNode.Stage.NotConnected;
        typeof(NetNode).GetProperty(nameof(NetNode.PendingAdmissionChallenge))!.SetValue(fixture.Node, null);
        ui.Update();
        Assert.True(ui.IsWaitingForDetachedAuthentication);
        if (cancelled) { ui.Dispose(); }

        completed.SetResult(fixture.Result);
        ui.Update();
        var recovery = ui.TakeReconnectAuthorization();
        if (cancelled) { Assert.Null(recovery); }
        else
        {
            Assert.NotNull(recovery);
            Assert.Equal(fixture.Scope, recovery.Scope);
            Assert.Equal(fixture.Result.Grant, recovery.Grant);
            Assert.Null(ui.TakeReconnectAuthorization());
        }

        Assert.Null(fixture.Node.TemporaryAuthentication);
        Assert.False(fixture.Node.AdmissionAccepted);
    }

    [Fact]
    public void BrowserApprovalAlreadySubmittedCanRecoverItsGrantBeforeAdmissionIsAccepted()
    {
        using var fixture = new Fixture();
        using var ui = fixture.CreateUi();
        Set(ui, "_browserOpened", true);
        Set(ui, "_authorization", Task.FromResult(fixture.Result));
        ui.Update();
        Assert.False(fixture.Node.AdmissionAccepted);
        Assert.True(
            (bool)typeof(ClientAdmissionUi)
                .GetMethod("PreserveExternalAuthentication", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(ui, null)!
        );
        fixture.Node.Stop();
        var recovery = ui.TakeReconnectAuthorization();
        Assert.NotNull(recovery);
        Assert.Equal(fixture.Result.Grant, recovery.Grant);
        Assert.Null(ui.TakeReconnectAuthorization());
        Assert.Null(fixture.Node.TemporaryAuthentication);
    }

    [Fact]
    public void HttpTaskTimeoutReportsServiceUnavailableInsteadOfAuthenticationDenied()
    {
        using var fixture = new Fixture();
        using var ui = fixture.CreateUi();
        Set(ui, "_authorization", Task.FromCanceled<AuthenticationLoginResult>(new CancellationToken(true)));
        ui.Update();
        Assert.Equal(LanguageManager.GetContentWidgets(nameof(ClientAdmissionUi), "Unavailable"), fixture.Node.Error);
        Assert.Null(ui.TakeReconnectAuthorization());
    }

    private static void Set(object target, string field, object? value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly WorkType _previousMode = CommonLib.WorkType;

        private readonly ECDsa _provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        private readonly ECDsa _user = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        private readonly ECDsa _server = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        internal NetNode Node { get; } = new() { CurrentStage = NetNode.Stage.Bootstrapping };

        internal string Scope { get; } =
            AuthenticationJoinScope.ForRemote(new Engine.Network.AddressNetworkTarget("game.example", 28887));

        internal AuthenticationLoginResult Result { get; private set; } = null!;

        internal ClientAdmissionUi CreateUi()
        {
            var now = DateTimeOffset.UtcNow;
            var endpoint = new AuthenticationServiceEndpoint(
                new AuthenticationServiceDescription(
                    AuthenticationProtocol.Version,
                    "Authority",
                    "https://auth.example/",
                    AuthenticationPublicKey.From(_provider).EncodedKey
                )
            );
            var subject = AuthenticationPublicKey.From(_user).Address;
            var server = AuthenticationPublicKey.From(_server).Address;
            var grant = new StoredAuthenticationGrant(
                subject,
                endpoint.Identity.Address,
                endpoint.BaseUri.AbsoluteUri,
                AuthenticationNonce.Create(),
                now.AddDays(1).ToUnixTimeSeconds()
            );
            var temporary = new TemporaryJoinAuthorization(
                subject,
                server,
                Scope,
                endpoint,
                grant
            );
            // Prepare completed HTTP login input independently of widgets; the real local admission still verifies its signed proof.
            var ui = new ClientAdmissionUi(Node, new Screen(), Scope, temporary);
            var local = new LocalHostAdmissionSession(
                "fixture-world",
                ServerAdmissionPolicy.WithAuthority(endpoint),
                subject,
                server,
                now
            );
            Set(Node, "_localAdmission", local);
            typeof(NetNode).GetProperty(nameof(NetNode.PendingAdmissionChallenge))!.SetValue(Node, local.Describe());
            Set(ui, "_challenge", local.Describe());
            Set(ui, "_endpoint", endpoint);
            Set(ui, "_authorizationChallenge", local.Challenge);
            Result = new AuthenticationLoginResult(
                grant,
                AdmissionApprovalTokens.Issue(_provider, local.Challenge, now)
            );
            return ui;
        }

        public void Dispose()
        {
            CommonLib.WorkType = _previousMode;
            _provider.Dispose();
            _user.Dispose();
            _server.Dispose();
        }
    }
}
