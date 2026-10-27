using GarageSaas.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class DeploymentStorageTests
{
    [TestMethod]
    public void KeyRingSurvivesNewProviderAndRemainsIsolatedByApplicationName()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GarageSaas-keys-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var first = DataProtectionProvider.Create(new DirectoryInfo(directory), options => options.SetApplicationName("GarageSaas.Staging"));
            var token = first.CreateProtector("garage-context").Protect("synthetic-user:synthetic-garage");
            var restarted = DataProtectionProvider.Create(new DirectoryInfo(directory), options => options.SetApplicationName("GarageSaas.Staging"));
            Assert.AreEqual("synthetic-user:synthetic-garage", restarted.CreateProtector("garage-context").Unprotect(token));
            var other = DataProtectionProvider.Create(new DirectoryInfo(directory), options => options.SetApplicationName("GarageSaas.Production"));
            Assert.ThrowsException<CryptographicException>(() => other.CreateProtector("garage-context").Unprotect(token));
        }
        finally { Directory.Delete(directory, true); }
    }
    [DataTestMethod]
    [DataRow("relative/path")]
    [DataRow("%GARAGESAAS_UNDEFINED_HOME%/data")]
    public void PersistentDirectoriesRejectRelativeAndUnresolvedPaths(string value)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Storage:GarageLogoDirectory"] = value }).Build();
        var error = Assert.ThrowsException<InvalidOperationException>(() => DeploymentStorage.DirectoryPath(config, "Storage:GarageLogoDirectory"));
        Assert.IsFalse(error.Message.Contains(value));
    }
    [TestMethod]
    public void KeysCannotBePlacedInPublicLogoOrWebRootDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "GarageSaas-public-test");
        Assert.ThrowsException<InvalidOperationException>(() => DeploymentStorage.ValidatePrivateKeys(Path.Combine(root, "keys"), root, null));
        Assert.ThrowsException<InvalidOperationException>(() => DeploymentStorage.ValidatePrivateKeys(root, null, root));
        DeploymentStorage.ValidatePrivateKeys(Path.Combine(root, "private-keys"), Path.Combine(root, "logos"), Path.Combine(root, "wwwroot"));
    }
}
