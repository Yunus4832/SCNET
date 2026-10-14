using System.Xml.Linq;

using Engine.Network;

namespace Game.Dialogs;

public sealed class JoinCodeDialog : Dialog
{
    private readonly TextBoxWidget _address;
    private readonly TextBoxWidget _code;
    private readonly ButtonWidget _relay;
    private RelayPolicy _relayPolicy;
    private readonly LabelWidget _error;
    private readonly CanvasWidget _surface;
    private readonly Action<NetworkTarget> _connect;

    public JoinCodeDialog(RelayPolicy initialPolicy, Action<NetworkTarget> connect)
    {
        _connect = connect;
        LoadContents(this, ContentManager.Get<XElement>("Dialogs/JoinCodeDialog"));
        _address = Children.Find<TextBoxWidget>("Address")!;
        _code = Children.Find<TextBoxWidget>("Code")!;
        _relay = Children.Find<ButtonWidget>("Relay")!;
        _relayPolicy = initialPolicy;
        _error = Children.Find<LabelWidget>("Error")!;
        _surface = Children.Find<CanvasWidget>("Surface")!;
        _address.MaximumLength = 2048;
        _code.MaximumLength = 6;
        _address.HasFocus = true;
        _address.Enter += _ => _code.HasFocus = true;
        _code.Enter += _ => TryConnect();
        _address.TextChanged += _ => _error.Text = string.Empty;
        _code.TextChanged += _ => _error.Text = string.Empty;
    }

    public override void Update()
    {
        if (_relay.IsClicked)
        {
            _relayPolicy = (RelayPolicy)(((int)_relayPolicy + 1) % Enum.GetValues<RelayPolicy>().Length);
        }
        _relay.Text = P2PRoomStatusDialog.Text($"Relay{_relayPolicy}");
        _error.IsVisible = !string.IsNullOrEmpty(_error.Text);
        _surface.Size = new Vector2(560f, _error.IsVisible ? 352f : 324f);
        if (Children.Find<ButtonWidget>("Connect")!.IsClicked)
        {
            TryConnect();
        }
        else if (Input.Cancel || Input.Back || Children.Find<ButtonWidget>("Cancel")!.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
    }

    private void TryConnect()
    {
        Uri address;
        try
        {
            address = RendezvousAddress.Normalize(new Uri(_address.Text.Trim()));
        }
        catch (Exception exception) when (exception is ArgumentException or UriFormatException)
        {
            _error.Text = LanguageManager.GetContentWidgets(nameof(JoinCodeDialog), "InvalidAddress");
            return;
        }
        InvitationCodeNetworkTarget target;
        try
        {
            target = new InvitationCodeNetworkTarget(address, _code.Text.Trim(), _relayPolicy);
        }
        catch (ArgumentException)
        {
            _error.Text = P2PRoomStatusDialog.Text("InvalidCode");
            return;
        }
        DialogsManager.HideDialog(this);
        _connect(target);
    }
}
