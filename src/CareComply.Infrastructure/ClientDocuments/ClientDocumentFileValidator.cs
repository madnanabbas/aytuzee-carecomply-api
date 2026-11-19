using System.Security.Cryptography;
using CareComply.Application.Abstractions.ClientDocuments;
using UglyToad.PdfPig;

namespace CareComply.Infrastructure.ClientDocuments;

public sealed class ClientDocumentFileValidator : IClientDocumentFileValidator
{
    internal const long MaximumBytes = 10 * 1024 * 1024;

    public async Task<ClientDocumentFileValidationResult> ValidateAsync(Stream content, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"carecomply-document-{Guid.CreateVersion7():N}.tmp");
        var retained = false;
        try
        {
            long size = 0;
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    size += read;
                    if (size > MaximumBytes) return new(null, ClientDocumentFileValidationError.TooLarge);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            if (size == 0) return new(null, ClientDocumentFileValidationError.Malformed);
            var type = await DetectAsync(path, cancellationToken);
            if (type.Error is { } error) return new(null, error);
            await using var hashStream = File.OpenRead(path);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, cancellationToken)).ToLowerInvariant();
            retained = true;
            return new(new ValidatedFile(path, type.ContentType!, type.Extension!, size, hash), null);
        }
        finally { if (!retained && File.Exists(path)) File.Delete(path); }
    }

    private static async Task<(string? ContentType, string? Extension, ClientDocumentFileValidationError? Error)> DetectAsync(string path, CancellationToken ct)
    {
        var bytes = new byte[16];
        await using var source = File.OpenRead(path);
        var count = await source.ReadAsync(bytes, ct);
        if (count >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
        {
            try
            {
                using var pdf = PdfDocument.Open(path);
                if (pdf.NumberOfPages < 1) return (null, null, ClientDocumentFileValidationError.Malformed);
                return ("application/pdf", ".pdf", null);
            }
            catch { return (null, null, ClientDocumentFileValidationError.Malformed); }
        }
        if (count >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return await ValidPng(path, ct) ? ("image/png", ".png", null) : (null, null, ClientDocumentFileValidationError.Malformed);
        if (count >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            return await ValidJpeg(path, ct) ? ("image/jpeg", ".jpg", null) : (null, null, ClientDocumentFileValidationError.Malformed);
        return (null, null, ClientDocumentFileValidationError.UnsupportedType);
    }

    private static async Task<bool> ValidPng(string path, CancellationToken ct)
    {
        var data = await File.ReadAllBytesAsync(path, ct);
        if (data.Length < 33 || !data.AsSpan(12, 4).SequenceEqual("IHDR"u8)) return false;
        var offset = 8;
        while (offset + 12 <= data.Length)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
            if (length > int.MaxValue || offset + 12L + length > data.Length) return false;
            if (data.AsSpan(offset + 4, 4).SequenceEqual("IEND"u8)) return length == 0 && offset + 12 == data.Length;
            offset += checked((int)length + 12);
        }
        return false;
    }

    private static async Task<bool> ValidJpeg(string path, CancellationToken ct)
    {
        var data = await File.ReadAllBytesAsync(path, ct);
        return data.Length >= 4 && data[^2] == 0xFF && data[^1] == 0xD9
            && (data.AsSpan(2, data.Length - 4).IndexOf("JFIF"u8) >= 0
                || data.AsSpan(2, data.Length - 4).IndexOf("Exif"u8) >= 0);
    }

    private sealed class ValidatedFile(string path, string contentType, string extension, long size, string hash)
        : IValidatedClientDocumentFile
    {
        public string ContentType { get; } = contentType;
        public string Extension { get; } = extension;
        public long SizeBytes { get; } = size;
        public string Sha256 { get; } = hash;
        public Stream OpenRead() => File.OpenRead(path);
        public ValueTask DisposeAsync() { if (File.Exists(path)) File.Delete(path); return ValueTask.CompletedTask; }
    }
}
