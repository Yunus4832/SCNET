using System.Text.Json;
using System.Text.Json.Nodes;

using Game.ContentProviders;

namespace Game.Dialogs;

public class LoginDialog : Dialog
{
    private TextBoxWidget _accountInput = null!;

    private TextBoxWidget _passwordInput = null!;

    private BevelledButtonWidget _loginBtn = null!;

    private BevelledButtonWidget _regBtn = null!;

    private BevelledButtonWidget _cancelBtn = null!;

    private readonly StackPanelWidget _mainView = new()
    {
        Direction = LayoutDirection.Vertical,
        HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Near,
        Margin = new Vector2(10f, 10f)
    };

    private readonly LabelWidget _tip = new()
    {
        HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Near,
        Size = new Vector2(-1, 48)
    };

    public override WidgetAlignment HorizontalAlignment { get; set; } = WidgetAlignment.Center;

    public override WidgetAlignment VerticalAlignment { get; set; } = WidgetAlignment.Center;

    public LoginDialog()
    {
        Size = new Vector2(600f, 320f);
        var rectangleWidget = new RectangleWidget
        {
            FillColor = new Color(0, 0, 0, 255),
            OutlineColor = new Color(128, 128, 128, 128),
            OutlineThickness = 2
        };
        Children.Add(rectangleWidget);
        Children.Add(_mainView);
        _mainView.Children.Add(_tip);
        _mainView.Children.Add(MakeTextBox("账号:"));
        _mainView.Children.Add(MakeTextBox("密码:"));
        _mainView.Children.Add(MakeButton());
    }

    public void OnFail(Exception ex)
    {
        _loginBtn.Text = "登录";
        DialogsManager.ShowDialog(
            null,
            new MessageDialog(
                LanguageManager.Error,
                "登录失败:" + ex.Message,
                LanguageManager.Ok,
                string.Empty,
                delegate { DialogsManager.HideAllDialogs(); }
            )
        );
    }

    public void OnSuccess(byte[] result)
    {
        _loginBtn.Text = "登录";
        var streamReader = new StreamReader(new MemoryStream(result));
        var json = JsonSerializer.Deserialize<JsonObject>(streamReader.ReadToEnd());

        if (json is null)
        {
            _tip.Text = "Deserialize response failed";
            ShowLoginResponseHandleFailedDialog(_tip.Text);
            return;
        }

        if (!json.TryGetPropertyValue("code", out var codeNode) || codeNode is null)
        {
            _tip.Text = "Response code not found";
            ShowLoginResponseHandleFailedDialog(_tip.Text);
            return;
        }

        if (!json.TryGetPropertyValue("msg", out var msgNode))
        {
            _tip.Text = "Response msg not found";
            ShowLoginResponseHandleFailedDialog(_tip.Text);
            return;
        }

        var code = int.Parse(codeNode.ToString());
        var msg = msgNode?.ToString() ?? string.Empty;

        if (code != 200)
        {
            _tip.Text = msg;
            ShowLoginResponseHandleFailedDialog(_tip.Text);
            return;
        }

        if (!json.TryGetPropertyValue("data", out var dataNode) || dataNode is not JsonObject dataObj)
        {
            _tip.Text = "Response data not found";
            ShowLoginResponseHandleFailedDialog(_tip.Text);
            return;
        }

        if (!dataObj.TryGetPropertyValue("accessToken", out var accessTokenNode) || accessTokenNode is null)
        {
            _tip.Text = "Data accessToken not found";
            ShowLoginResponseHandleFailedDialog(_tip.Text);
            return;
        }

        SettingsManager.Current.CommunityAccessUser = _accountInput.Text;
        SettingsManager.Current.CommunityAccessToken = accessTokenNode.ToString();
        SettingsManager.Current.OnlineAccessToken = HashUtils.ComputeMd5($"{_accountInput.Text}{_passwordInput.Text}");
        SettingsManager.Current.CommunityNickName = dataObj["nickName"]?.ToString() ?? string.Empty;
        SettingsManager.Current.ScpboxUserInfo = string.Empty;
        SettingsManager.Current.ScpboxUserInfo += "昵称：" + dataObj["nickName"];
        SettingsManager.Current.ScpboxUserInfo += "\n账号：" + dataObj["user"];
        SettingsManager.Current.ScpboxUserInfo += "\n登录时间：" + dataObj["loginTime"];
        DialogsManager.ShowDialog(
            null,
            new MessageDialog(
                LanguageManager.Ok,
                "登录成功:" + dataObj["nickName"],
                LanguageManager.Ok,
                string.Empty,
                delegate { DialogsManager.HideAllDialogs(); }
            )
        );
    }

    /// <summary>
    /// 显示登录响应失败对话框
    /// </summary>
    private void ShowLoginResponseHandleFailedDialog(string reason)
    {
        DialogsManager.ShowDialog(
            null,
            new MessageDialog(
                LanguageManager.Ok,
                "登录失败:" + reason,
                LanguageManager.Ok,
                string.Empty,
                delegate { DialogsManager.HideAllDialogs(); }
            )
        );
    }

    public Widget MakeTextBox(string title)
    {
        var canvasWidget = new CanvasWidget { Margin = new Vector2(10f) };
        var rectangleWidget = new RectangleWidget { FillColor = Color.Black, OutlineColor = Color.White };
        var stack = new StackPanelWidget
            { Direction = LayoutDirection.Horizontal, VerticalAlignment = WidgetAlignment.Center };
        var label = new LabelWidget
        {
            HorizontalAlignment = WidgetAlignment.Near, VerticalAlignment = WidgetAlignment.Center, Text = title,
            Size = new Vector2(80f, -1f)
        };
        var textBox = new TextBoxWidget
        {
            HorizontalAlignment = WidgetAlignment.Near, VerticalAlignment = WidgetAlignment.Center,
            Color = new Color(255, 255, 255), Margin = new Vector2(4f, 0f),
            Size = new Vector2(float.PositiveInfinity, 80)
        };
        if (title == "账号:")
        {
            _accountInput = textBox;
        }

        if (title == "密码:")
        {
            _passwordInput = textBox;
        }

        stack.Children.Add(label);
        stack.Children.Add(canvasWidget);
        canvasWidget.Children.Add(rectangleWidget);
        canvasWidget.Children.Add(textBox);
        return stack;
    }

    public Widget MakeButton()
    {
        var stack = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal, Margin = new Vector2(0, 10),
            HorizontalAlignment = WidgetAlignment.Center
        };
        _loginBtn = new BevelledButtonWidget { Size = new Vector2(160, 60), Margin = new Vector2(4f, 0), Text = "登陆" };
        _regBtn = new BevelledButtonWidget { Size = new Vector2(160, 60), Margin = new Vector2(4f, 0), Text = "注册" };
        _cancelBtn = new BevelledButtonWidget { Size = new Vector2(160, 60), Margin = new Vector2(4f, 0), Text = "取消" };
        stack.Children.Add(_loginBtn);
        stack.Children.Add(_regBtn);
        stack.Children.Add(_cancelBtn);
        return stack;
    }

    public override void Update()
    {
        if (_loginBtn.IsClicked)
        {
            _loginBtn.Text = "登录中...";
            var par = new Dictionary<string, string>
            {
                { "user", _accountInput.Text },
                { "pass", _passwordInput.Text }
            };
            WebManager.Post(
                SchubExternalContentProvider.RedirectUri + "/com/api/login",
                par,
                new Dictionary<string, string>(),
                new MemoryStream(),
                new CancellableProgress(),
                OnSuccess,
                OnFail
            );
        }

        if (_regBtn.IsClicked)
        {
            WebBrowserManager.LaunchBrowser("www.schub.top" + "/reg");
        }

        if (_cancelBtn.IsClicked)
        {
            DialogsManager.HideAllDialogs();
        }
    }
}
