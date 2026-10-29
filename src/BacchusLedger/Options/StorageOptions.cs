namespace BacchusLedger.Options;

/// Where data lives. Bound in Startup from the same environment variables as before (DB_PATH, RESET_ON_START, SIGNING_KEY_PATH).
public sealed class StorageOptions
{
    public string DbPath { get; set; } = "/data/bacchus.db";

    /// Drop and re-seed all demo data on start.
    public bool ResetOnStart { get; set; }

    /// DEMO signing key (PEM). Generated on first use when the file is missing.
    public string SigningKeyPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "keys", "DEMO_KEY_ecdsa_p256_NOT_FOR_PRODUCTION.pem");
}
