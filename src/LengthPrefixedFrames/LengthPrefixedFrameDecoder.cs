using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;

namespace LengthPrefixedFrames;

public sealed class LengthPrefixedFrameDecoder
{
    private readonly int _maxFrameLength;

    public LengthPrefixedFrameDecoder(int maxFrameLength = 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxFrameLength);
        _maxFrameLength = maxFrameLength;
    }

    /// <summary>
    /// Reads the next frame. Returns <see langword="null"/> after a clean end of stream.
    /// </summary>
    public async ValueTask<byte[]?> ReadFrameAsync(
        PipeReader reader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        while (true)
        {
            ReadResult result = await reader
                .ReadAsync(cancellationToken)
                .ConfigureAwait(false);

            ReadOnlySequence<byte> buffer = result.Buffer;
            SequencePosition consumed = buffer.Start;
            SequencePosition examined = buffer.Start;

            try
            {
                if (result.IsCanceled)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (TryReadFrame(ref buffer, _maxFrameLength, out byte[]? frame))
                {
                    // Leave any following bytes unexamined for the next call.
                    consumed = buffer.Start;
                    examined = buffer.Start;
                    return frame;
                }

                if (result.IsCompleted)
                {
                    consumed = buffer.End;
                    examined = buffer.End;

                    if (!buffer.IsEmpty)
                    {
                        throw new InvalidDataException(
                            "The stream ended in the middle of a frame.");
                    }

                    return null;
                }

                examined = buffer.End;
            }
            finally
            {
                reader.AdvanceTo(consumed, examined);
            }
        }
    }

    private static bool TryReadFrame(
        ref ReadOnlySequence<byte> buffer,
        int maxFrameLength,
        [NotNullWhen(true)] out byte[]? frame)
    {
        frame = null;
        var reader = new SequenceReader<byte>(buffer);

        if (!reader.TryReadBigEndian(out int length))
        {
            return false;
        }

        // Validate the wire value before allocating for it.
        if (length < 0 || length > maxFrameLength)
        {
            throw new InvalidDataException(
                $"Invalid frame length {length}. The maximum is {maxFrameLength}.");
        }

        if (reader.Remaining < length)
        {
            return false;
        }

        frame = new byte[length];
        reader.UnreadSequence.Slice(0, length).CopyTo(frame);
        reader.Advance(length);
        buffer = buffer.Slice(reader.Position);
        return true;
    }
}
