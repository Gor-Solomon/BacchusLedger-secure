namespace BacchusLedger.Providers;

public interface IQrCodeRenderer
{
    /// The text as a QR code PNG, as a data: URL.
    string ToPngDataUrl(string text);
}
