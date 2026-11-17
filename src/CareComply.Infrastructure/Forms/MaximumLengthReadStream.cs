namespace CareComply.Infrastructure.Forms;

internal sealed class MaximumLengthReadStream(Stream inner, long maximumLength) : Stream
{
    private long bytesRead;

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => bytesRead;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        RecordRead(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = inner.Read(buffer);
        RecordRead(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        RecordRead(read);
        return read;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        ReadArrayAsync(buffer, offset, count, cancellationToken);

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        // The caller owns the source stream.
        base.Dispose(disposing);
    }

    private async Task<int> ReadArrayAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        RecordRead(read);
        return read;
    }

    private void RecordRead(int count)
    {
        bytesRead += count;
        if (bytesRead > maximumLength)
            throw new InvalidDataException("Form content exceeds the configured maximum upload size.");
    }
}
