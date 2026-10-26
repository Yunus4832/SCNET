using System.Xml.Linq;

using Game.Authentication;
using Game.Identity;

using SCNET.Authentication.Protocol.Http;

namespace Game.Dialogs;

public sealed class AuthenticationConsentDialog : Dialog
{
    private readonly Action<AuthenticationTrustChoice> _complete;

    private readonly string _identityDetails;

    private bool _closed;

    public AuthenticationConsentDialog(
        AuthenticationServiceEndpoint endpoint,
        string serverAddress,
        string descriptionKey,
        Action<AuthenticationTrustChoice> complete
    )
    {
        _complete = complete;
        _identityDetails = string.Format(Text("Fingerprint"), endpoint.Identity.Address) +
                           "\n" +
                           string.Format(Text("Server"), serverAddress);
        LoadContents(this, ContentManager.Get<XElement>("Dialogs/AuthenticationConsentDialog"));
        Children.Find<LabelWidget>("Description")!.Text = Text(descriptionKey);
        Children.Find<LabelWidget>("Service")!.Text = endpoint.Description.DisplayName;
        Children.Find<LabelWidget>("Url")!.Text = endpoint.BaseUri.AbsoluteUri;
        Children.Find<LabelWidget>("Fingerprint")!.Text = string.Format(
            Text("Fingerprint"),
            IdentityAddressDisplay.Abbreviate(endpoint.Identity.Address)
        );
        Children.Find<LabelWidget>("Server")!.Text = string.Format(
            Text("Server"),
            IdentityAddressDisplay.Abbreviate(serverAddress)
        );
    }

    public override void Update()
    {
        if (Children.Find<ButtonWidget>("Details")!.IsClicked)
        {
            DialogsManager.ShowDialog(
                this,
                new MessageDialog(
                    Text("Details"),
                    _identityDetails,
                    LanguageManager.GetContentWidgets("SettingsIdentityScreen", "Copy"),
                    LanguageManager.Get("Usual", "ok"),
                    button =>
                    {
                        if (button == MessageDialogButton.Button1)
                        {
                            ClipboardManager.ClipboardString = _identityDetails;
                        }
                    }
                )
            );
        }
        else if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("Reject")!.IsClicked) { Cancel(); }
        else if (Children.Find<ButtonWidget>("Save")!.IsClicked) { Complete(AuthenticationTrustChoice.LoginAndSave); }
        else if (Children.Find<ButtonWidget>("Once")!.IsClicked) { Complete(AuthenticationTrustChoice.LoginOnce); }
    }

    public void Cancel() => Complete(AuthenticationTrustChoice.Reject);

    private void Complete(AuthenticationTrustChoice choice)
    {
        if (_closed) { return; }

        _closed = true;
        DialogsManager.HideDialog(this);
        _complete(choice);
    }

    private static string Text(string key) =>
        LanguageManager.GetContentWidgets(nameof(AuthenticationConsentDialog), key);
}
