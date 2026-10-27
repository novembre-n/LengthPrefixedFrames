using System.IO.Pipelines;
using Xunit;

namespace LengthPrefixedFrames.Tests;

public sealed class LengthPrefixedFrameDecoderTests
{
    [Fact]
    public async Task Reads_frame_written_in_fragments()
    {
        var pipe = new Pipe();
        var decoder = new LengthPrefixedFrameDecoder();

        Task<byte[]?> pendingRead = decoder
            .ReadFrameAsync(pipe.Reader)
            .AsTask();

        await pipe.Writer.WriteAsync(new byte[] { 0, 0 });
        await pipe.Writer.WriteAsync(new byte[] { 0, 3, 10 });
        await pipe.Writer.WriteAsync(new byte[] { 20, 30 });
        await pipe.Writer.CompleteAsync();

        Assert.Equal(
            new byte[] { 10, 20, 30 },
            await pendingRead);

        await pipe.Reader.CompleteAsync();
    }

    [Fact]
    public async Task Reads_multiple_frames_from_one_buffer()
    {
        var pipe = new Pipe();
        var decoder = new LengthPrefixedFrameDecoder();

        await pipe.Writer.WriteAsync(new byte[]
        {
            0, 0, 0, 2, 10, 20,
            0, 0, 0, 1, 30
        });
        await pipe.Writer.CompleteAsync();

        Assert.Equal(
            new byte[] { 10, 20 },
            await decoder.ReadFrameAsync(pipe.Reader));

        Assert.Equal(
            new byte[] { 30 },
            await decoder.ReadFrameAsync(pipe.Reader));

        Assert.Null(await decoder.ReadFrameAsync(pipe.Reader));

        await pipe.Reader.CompleteAsync();
    }

    [Fact]
    public async Task Reads_empty_frame()
    {
        var pipe = new Pipe();
        var decoder = new LengthPrefixedFrameDecoder();

        await pipe.Writer.WriteAsync(new byte[] { 0, 0, 0, 0 });
        await pipe.Writer.CompleteAsync();

        Assert.Equal(
            Array.Empty<byte>(),
            await decoder.ReadFrameAsync(pipe.Reader));

        Assert.Null(await decoder.ReadFrameAsync(pipe.Reader));

        await pipe.Reader.CompleteAsync();
    }

    [Fact]
    public void Rejects_negative_maximum_frame_length()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LengthPrefixedFrameDecoder(maxFrameLength: -1));
    }

    [Fact]
    public async Task Rejects_truncated_length_prefix_at_end_of_stream()
    {
        var pipe = new Pipe();
        var decoder = new LengthPrefixedFrameDecoder();

        await pipe.Writer.WriteAsync(new byte[] { 0, 0 });
        await pipe.Writer.CompleteAsync();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => decoder.ReadFrameAsync(pipe.Reader).AsTask());

        await pipe.Reader.CompleteAsync();
    }

    [Fact]
    public async Task Rejects_incomplete_frame_at_end_of_stream()
    {
        var pipe = new Pipe();
        var decoder = new LengthPrefixedFrameDecoder();

        await pipe.Writer.WriteAsync(new byte[] { 0, 0, 0, 3, 10 });
        await pipe.Writer.CompleteAsync();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => decoder.ReadFrameAsync(pipe.Reader).AsTask());

        await pipe.Reader.CompleteAsync();
    }

    [Fact]
    public async Task Rejects_frame_larger_than_configured_limit()
    {
        var pipe = new Pipe();
        var decoder = new LengthPrefixedFrameDecoder(maxFrameLength: 2);

        await pipe.Writer.WriteAsync(new byte[] { 0, 0, 0, 3 });
        await pipe.Writer.CompleteAsync();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => decoder.ReadFrameAsync(pipe.Reader).AsTask());

        await pipe.Reader.CompleteAsync();
    }
}
