# LengthPrefixedFrames

A small .NET 10 library that turns a byte stream into complete messages. It is useful for custom protocols over TCP, named pipes, or any other stream wrapped in a `PipeReader`.

> Status: experimental reference implementation. It demonstrates stream framing and is not a complete networking library.

## Wire format

Each frame contains a 4-byte length and a payload.

| Offset | Size | Field |
| --- | --- | --- |
| `0` | 4 bytes | Payload length, signed 32-bit big-endian |
| `4` | N bytes | Payload |

The length does not include the 4-byte prefix. Empty payloads are valid. The default maximum payload size is 1 MiB and is configurable in the constructor.

## Byte examples

The text `hello` has five bytes, so its frame is:

```text
00 00 00 05 | 68 65 6C 6C 6F
length = 5  | payload = "hello"
```

An empty frame contains only the length prefix:

```text
00 00 00 00
length = 0
```

Two frames can arrive together:

```text
00 00 00 03 41 42 43 | 00 00 00 02 44 45
length 3, "ABC"      | length 2, "DE"
```

The decoder returns `"ABC"` and then `"DE"` as two separate payloads.

Fragmented input is also valid. These three reads form one frame:

```text
Read 1: 00 00
Read 2: 00 05 68 65
Read 3: 6C 6C 6F
```

The decoder waits until the fourth length byte and all five payload bytes are available. It then returns `"hello"`.

Invalid input fails immediately. A negative length such as `FF FF FF FF` is rejected. A length of `00 10 00 01` exceeds the default limit of 1 MiB and is also rejected.

If the stream ends after only part of a frame, the decoder throws `InvalidDataException`. For example, the prefix below announces five payload bytes, but the stream closes after two:

```text
00 00 00 05 68 65 | end of stream
```

## How it works

A stream does not preserve message boundaries. One read can contain part of a frame, one complete frame, or several frames.

The decoder:

- Waits when the length prefix or payload is incomplete.
- Returns complete frames one at a time.
- Leaves bytes for the following frame in the pipe.
- Copies each payload into a `byte[]` owned by the caller.
- Rejects an invalid length before allocating memory.
- Throws when the stream ends in the middle of a frame.

Only one caller must read from a given `PipeReader` at a time. The caller owns the reader and all returned payloads.

## Usage

1. Reference `LengthPrefixedFrames.csproj` from the application.
2. Wrap the input stream in a `PipeReader`.
3. Create a `LengthPrefixedFrameDecoder` for the connection.
4. Read frames until the decoder returns `null`.
5. Interpret each returned payload as text, JSON, protobuf, or binary data.

The decoder only handles frame boundaries. It does not encrypt, authenticate, or interpret the payload.

To send a compatible message, write the payload length as four big-endian bytes, then write the payload bytes.

## Behavior

| Condition | Result |
| --- | --- |
| Clean end of stream before a new frame | `null` |
| Incomplete frame and the stream is open | Waits for more data |
| Negative length or payload above the limit | `InvalidDataException` |
| Stream ends in the middle of a frame | `InvalidDataException` |
| Read is canceled | `OperationCanceledException` |

After `InvalidDataException`, close the connection. Do not continue reading from the same stream.

## Experiment

The test project is the runnable example. It uses an in-memory pipe, so no network connection is required. The tests cover fragmented frames, multiple frames in one buffer, empty frames, invalid lengths, and truncated input.

Requirements: .NET 10 SDK.

Run `dotnet test` from the repository root.
