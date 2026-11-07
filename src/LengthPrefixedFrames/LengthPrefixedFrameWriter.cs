using System.Buffers.Binary;
using System.IO.Pipelines;

namespace LengthPrefixedFrames;

public static class LengthPrefixedFrameWriter
{
    /// <summary>
    /// Writes one frame and makes it available to the reader.
    /// </summary>
    /// <returns>
    /// The result of the flush. Check <see cref="FlushResult.IsCompleted"/> to detect a closed reader.
    /// </returns>
    public static async ValueTask<FlushResult> WriteFrameAsync(
        PipeWriter writer,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);

        WriteLengthPrefix(writer, payload.Length);

        return await writer
            .WriteAsync(payload, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void WriteLengthPrefix(PipeWriter writer, int payloadLength)
    {
        // Write into the pipe's own buffer to avoid an allocation per frame.
        BinaryPrimitives.WriteInt32BigEndian(writer.GetSpan(4), payloadLength);
        writer.Advance(4);
    }
}
