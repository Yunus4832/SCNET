using System.Reflection;

using Engine.Core.Logging;

using Game.Managers;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Authentication;

[Collection(ConfigFileCollection.Name)]
public sealed class AuthenticationBrowserPrivacyTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BrowserLaunchFailuresNeverLogTheUrlOrExceptionSecrets(bool throwingLauncher)
    {
        const string secret = "test-browser-fragment-secret";
        var field = typeof(WebBrowserManager).GetField("_launcher", BindingFlags.Static | BindingFlags.NonPublic)!;
        var original = field.GetValue(null);
        var previousMinimum = Log.MinimumLogType;
        var sink = new MemorySink();
        try
        {
            Log.MinimumLogType = LogType.Debug;
            Log.AddLogSink(sink);
            if (throwingLauncher)
            {
                WebBrowserManager.RegisterLauncher(_ => throw new InvalidOperationException(secret));
            }
            else { field.SetValue(null, null); }

            Assert.False(WebBrowserManager.LaunchBrowser("https://auth.example/login#" + secret));
            Assert.NotEmpty(sink.Messages);
            Assert.All(sink.Messages, message => Assert.DoesNotContain(secret, message));
            Assert.All(sink.Messages, message => Assert.DoesNotContain("auth.example", message));
        }
        finally
        {
            field.SetValue(null, original);
            Log.RemoveLogSink(sink);
            Log.MinimumLogType = previousMinimum;
        }
    }

    private sealed class MemorySink : ILogSink
    {
        internal List<string> Messages { get; } = [];

        public void Log(LogType type, string message) => Messages.Add(message);
    }
}
