namespace BacchusLedger.Application.Providers;

/// The DEMO ECDSA P-256 signing key (a local file outside the code and the image).
public interface ISigningKeyProvider
{
    string PublicKeyPem { get; }

    /// First 16 hex characters of the SHA-256 of the public key.
    string KeyId { get; }

    string Sign(string text);
    bool Verify(string text, string signature);
}
