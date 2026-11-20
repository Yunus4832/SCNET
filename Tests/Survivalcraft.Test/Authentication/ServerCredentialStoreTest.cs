using Engine.Core.Security;

using Game.Authentication;
using Game.Identity;

namespace Survivalcraft.Test.Authentication;

public sealed class ServerCredentialStoreTest
{
    [Fact]
    public void PasswordsPersistByServerIdentityAndCanBeForgottenWithoutChangingOtherServers()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-server-credentials-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "Private", "ServerCredentials.xml");
        var server = IdentityAddress.Encode(new byte[32]);
        var other = IdentityAddress.Encode(Enumerable.Repeat((byte)1, 32).ToArray());
        try
        {
            var store = new ServerCredentialStore(path);
            Assert.False(File.Exists(path));
            store.Set(server, "test password");
            Assert.Null(store.Find(other));
            store.Set(other, "second password");
            store = new ServerCredentialStore(path);
            Assert.Equal("test password", store.Find(server));
            store.Remove(server);
            store = new ServerCredentialStore(path);
            Assert.Null(store.Find(server));
            Assert.Equal("second password", store.Find(other));
            store.Clear();
            Assert.Equal(0, new ServerCredentialStore(path).Count);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            }
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void InvalidPasswordDoesNotReplaceTheCommittedValue()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-server-credentials-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "credentials.xml");
        var server = IdentityAddress.Encode(new byte[32]);
        try
        {
            var store = new ServerCredentialStore(path);
            store.Set(server, "original");
            Assert.Throws<InvalidDataException>(() => store.Set(server, "bad\npassword"));
            Assert.Equal("original", new ServerCredentialStore(path).Find(server));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void AddressAbbreviationRetainsUsefulPayloadAndCopySourceRemainsUnchanged()
    {
        var address = IdentityAddress.Encode(new byte[32]);
        Assert.Equal(address[..12] + "..." + address[^6..], IdentityAddressDisplay.Abbreviate(address));
        Assert.Equal(64, address.Length);
        Assert.Equal("yunus.top:9521", IdentityAddressDisplay.Abbreviate("yunus.top:9521"));
    }
}
