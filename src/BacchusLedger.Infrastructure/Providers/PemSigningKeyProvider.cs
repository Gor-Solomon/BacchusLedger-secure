using System.Security.Cryptography;
using System.Text;
using BacchusLedger.Application.Options;
using BacchusLedger.Application.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Infrastructure.Providers;

/// Loads the PEM key from StorageOptions.SigningKeyPath; generates a DEMO key there when the file is missing.
public sealed class PemSigningKeyProvider : ISigningKeyProvider
{
    readonly Lazy<ECDsa> key;

    public PemSigningKeyProvider(IOptions<StorageOptions> storage, ILogger<PemSigningKeyProvider> logger)
    {
        var path = storage.Value.SigningKeyPath;
        key = new(() =>
        {
            var k = ECDsa.Create();
            if (File.Exists(path)) { k.ImportFromPem(File.ReadAllText(path)); return k; }
            k = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "DEMO KEY - Bacchus Ledger secure copy - NOT FOR PRODUCTION. Generated " + DateTime.UtcNow.ToString("o") + "\n" + k.ExportPkcs8PrivateKeyPem() + "\n");
            logger.LogInformation("seal: generated DEMO signing key at {Path}", path);
            return k;
        });
    }

    public string PublicKeyPem => key.Value.ExportSubjectPublicKeyInfoPem();

    public string KeyId => Convert.ToHexString(SHA256.HashData(key.Value.ExportSubjectPublicKeyInfo()))[..16].ToLowerInvariant();

    public string Sign(string text) => Convert.ToBase64String(key.Value.SignData(Encoding.UTF8.GetBytes(text), HashAlgorithmName.SHA256));

    public bool Verify(string text, string signature)
    {
        try { return key.Value.VerifyData(Encoding.UTF8.GetBytes(text), Convert.FromBase64String(signature), HashAlgorithmName.SHA256); }
        catch (FormatException) { return false; }
    }
}
