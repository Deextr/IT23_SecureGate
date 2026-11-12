using QRCoder;

namespace SecureGate1.Services;

public static class QrCodeEncoder
{
    public static string ToDataUri(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(5);
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }
}
