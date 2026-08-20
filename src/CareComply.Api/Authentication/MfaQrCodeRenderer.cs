using QRCoder;

namespace CareComply.Api.Authentication;

public sealed class MfaQrCodeRenderer
{
    public string RenderPngDataUrl(string authenticatorUri)
    {
        using var qrCodeData = QRCodeGenerator.GenerateQrCode(
            authenticatorUri,
            QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var png = qrCode.GetGraphic(8);
        return $"data:image/png;base64,{Convert.ToBase64String(png)}";
    }
}
