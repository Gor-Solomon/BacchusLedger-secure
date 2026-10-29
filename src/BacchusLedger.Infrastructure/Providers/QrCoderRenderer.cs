using BacchusLedger.Application.Providers;
using QRCoder;

namespace BacchusLedger.Infrastructure.Providers;

/// QR codes with QRCoder (error correction M, 6 px modules).
public sealed class QrCoderRenderer : IQrCodeRenderer
{
    public string ToPngDataUrl(string text)
    {
        using var qr = new QRCodeGenerator();
        var png = new PngByteQRCode(qr.CreateQrCode(text, QRCodeGenerator.ECCLevel.M)).GetGraphic(6);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
