using System.Buffers;
using System.IO.Pipelines;
using Xunit;

namespace LengthPrefixedFrames.Tests;

public sealed class LengthPrefixedFrameWriterTests
{
    [Fact]
    public async Task Writes_big_endian_length_prefix_then_payload()
    {
        var pipe = new Pipe();

        await LengthPrefixedFrameWriter.WriteFrameAsync(
            pipe.Writer,
            new byte[] { 10, 20, 30 });
        await pipe.Writer.CompleteAsync();

        ReadResult result = await pipe.Reader.ReadAsync();

        Assert.Equal(
            new byte[] { 0, 0, 0, 3, 10, 20, 30 },
            result.Buffer.ToArray());

        pipe.Reader.AdvanceTo(result.Buffer.End);
        await pipe.Reader.CompleteAsync();
    }

    [Fact]
    public async Task Writes_empty_frame()
    {
        var pipe = new Pipe();

        await LengthPrefixedFrameWriter.WriteFrameAsync(
            pipe.Writer,
            ReadOnlyMemory<byte>.Empty);
        await pipe.Writer.CompleteAsync();

        ReadResult result = await pipe.Reader.ReadAsync();

        Assert.Equal(
            new byte[] { 0, 0, 0, 0 },
            result.Buffer.ToArray());

        pipe.Reader.AdvanceTo(result.Buffer.End);
        await pipe.Reader.CompleteAsync();
    }

    [Fact]
    public async Task Round_trips_payload_through_decoder()
    {
        var pipe = new Pipe();
        var decoder = new LengthPrefixedFrameDecoder();
        byte[] payload = "hello"u8.ToArray();

        await LengthPrefixedFrameWriter.WriteFrameAsync(pipe.Writer, payload);
        await pipe.Writer.CompleteAsync();

        Assert.Equal(payload, await decoder.ReadFrameAsync(pipe.Reader));
        Assert.Null(await decoder.ReadFrameAsync(pipe.Reader));

        await pipe.Reader.CompleteAsync();
    }
}
