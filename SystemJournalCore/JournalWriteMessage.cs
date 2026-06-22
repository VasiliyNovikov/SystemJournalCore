using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SystemJournalCore;

public ref struct JournalWriteMessage(Span<byte> scratchBuffer) : IDisposable
{
    private static readonly SearchValues<byte> FieldNameValidChars = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_"u8);

    private Span<byte> _buffer = scratchBuffer;
    private byte[]? _rentedBuffer;
    private int _length = 0;

    public readonly ReadOnlySpan<byte> Bytes => _buffer[.._length];

    public void Dispose()
    {
        if (_rentedBuffer is null)
            return;
        ArrayPool<byte>.Shared.Return(_rentedBuffer);
        _rentedBuffer = null;
    }

    public void Add(scoped ReadOnlySpan<byte> field, scoped ReadOnlySpan<byte> value) => value.CopyTo(PrepareAdd(field, value.Length, value.Contains((byte)'\n')));

    public void Add(scoped ReadOnlySpan<byte> field, string value) => ValueEncoder.GetBytes(value, PrepareAdd(field, ValueEncoder.GetByteCount(value), value.Contains('\n')));

    public void Add(string field, string value) => Add(FieldCache.Get(field), value);

    public void AddAll(IEnumerable<KeyValuePair<string, string>> fields)
    {
        foreach (var (field, value) in fields)
            Add(field, value);
    }

    private Span<byte> PrepareAdd(scoped ReadOnlySpan<byte> field, int valueLength, bool valueHasNewLine)
    {
        if (field.Length == 0)
            throw new ArgumentException("Field name cannot be empty", nameof(field));
        if (field[0] is < (byte)'A' or > (byte)'Z')
            throw new ArgumentException("Field name must start with an uppercase ASCII letter", nameof(field));
        if (field[1..].IndexOfAnyExcept(FieldNameValidChars) != -1)
            throw new ArgumentException("Field name contains invalid characters", nameof(field));
        Write(field);
        if (valueHasNewLine)
        {
            Write('\n');
            MemoryMarshal.Write(PrepareWrite(sizeof(ulong)), (ulong)valueLength);
        }
        else
            Write('=');
        var valueBuffer = PrepareWrite(valueLength);
        Write('\n');
        return valueBuffer;
    }

    private Span<byte> PrepareWrite(int length)
    {
        if (_length + length > _buffer.Length)
        {
            var previousRentedBuffer = _rentedBuffer;
            var newSize = Math.Max(_buffer.Length * 2, _length + length);
            _rentedBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            _buffer.CopyTo(_rentedBuffer);
            _buffer = _rentedBuffer;

            if (previousRentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(previousRentedBuffer);
        }
        var oldPosition = _length;
        _length += length;
        return _buffer[oldPosition.._length];
    }

    private void Write(scoped ReadOnlySpan<byte> value) => value.CopyTo(PrepareWrite(value.Length));
    private void Write(byte value) => Write([value]);
    private void Write(char value) => Write((byte)value);
}