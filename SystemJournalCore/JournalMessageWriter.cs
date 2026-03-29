using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SystemJournalCore;

public ref struct JournalMessageWriter(Span<byte> buffer) : IDisposable
{
    private static readonly SearchValues<byte> FieldNameValidChars = SearchValues.Create("ABCDEFGHIJKLMNOPRSTUVWZYZ0123456789_"u8);

    private Span<byte> _buffer = buffer;
    private byte[]? _rentedBuffer;
    private int _position;

    public readonly ReadOnlySpan<byte> Bytes => _buffer[.._position];

    public void Append(scoped ReadOnlySpan<byte> field, scoped ReadOnlySpan<byte> value)
    {
        if (field.Length == 0)
            throw new ArgumentException("Field name cannot be empty.", nameof(field));
        if (field[0] is < (byte)'A' or > (byte)'Z')
            throw new ArgumentException("Field name must start with an uppercase ASCII letter.", nameof(field));
        if (field[1..].IndexOfAnyExcept(FieldNameValidChars) != -1)
            throw new ArgumentException("Field name contains invalid characters.", nameof(field));
        Write(field);
        if (value.Contains((byte)'\n'))
        {
            Write('\n');
            MemoryMarshal.Write(PrepareWrite(sizeof(ulong)), (ulong)value.Length);
        }
        else
            Write('=');
        Write(value);
        Write('\n');
    }

    [SkipLocalsInit]
    public void Append(string field, string value)
    {
        Span<byte> fieldBytes = stackalloc byte[field.Length];
        Encoding.ASCII.GetBytes(field, fieldBytes);

        Span<byte> valueBytes = stackalloc byte[Encoding.UTF8.GetMaxByteCount(value.Length)];
        valueBytes = valueBytes[..Encoding.UTF8.GetBytes(value, valueBytes)];

        Append(fieldBytes, valueBytes);
    }

    public void AppendAll(IEnumerable<KeyValuePair<string, string>> fields)
    {
        foreach (var (field, value) in fields)
            Append(field, value);
    }

    public readonly void Dispose()
    {
        if (_rentedBuffer is not null)
            ArrayPool<byte>.Shared.Return(_rentedBuffer);
    }

    private Span<byte> PrepareWrite(int length)
    {
        if (_position + length > _buffer.Length)
        {
            var newSize = Math.Max(_buffer.Length * 2, _position + length);
            _rentedBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            _buffer.CopyTo(_rentedBuffer);
            _buffer = _rentedBuffer;
        }
        var oldPosition = _position;
        _position += length;
        return _buffer[oldPosition.._position];
    }

    private void Write(scoped ReadOnlySpan<byte> value) => value.CopyTo(PrepareWrite(value.Length));
    private void Write(byte value) => Write([value]);
    private void Write(char value) => Write((byte)value);
}