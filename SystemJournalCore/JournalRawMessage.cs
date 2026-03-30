using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SystemJournalCore;

public ref struct JournalRawMessage : IDisposable
{
    private static readonly SearchValues<byte> FieldNameValidChars = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_"u8);
    private static readonly SearchValues<byte> FieldSeparatorChars = SearchValues.Create("=\n"u8);

    private Span<byte> _buffer;
    private byte[]? _rentedBuffer;
    private int _length;

    public readonly ReadOnlySpan<byte> Bytes => _buffer[.._length];

    private JournalRawMessage(Span<byte> buffer, int length)
    {
        _buffer = buffer;
        _length = length;
    }

    public static JournalRawMessage FromBytes(Span<byte> buffer) => new(buffer, buffer.Length);

    public static JournalRawMessage New(Span<byte> buffer) => new(buffer, 0);

    public readonly void Dispose()
    {
        if (_rentedBuffer is not null)
            ArrayPool<byte>.Shared.Return(_rentedBuffer);
    }

    public void Add(scoped ReadOnlySpan<byte> field, scoped ReadOnlySpan<byte> value)
    {
        if (field.Length == 0)
            throw new ArgumentException("Field name cannot be empty", nameof(field));
        if (field[0] is < (byte)'A' or > (byte)'Z')
            throw new ArgumentException("Field name must start with an uppercase ASCII letter", nameof(field));
        if (field[1..].IndexOfAnyExcept(FieldNameValidChars) != -1)
            throw new ArgumentException("Field name contains invalid characters", nameof(field));
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
    public void Add(string field, string value)
    {
        Span<byte> fieldBytes = stackalloc byte[field.Length];
        Encoding.ASCII.GetBytes(field, fieldBytes);

        Span<byte> valueBytes = stackalloc byte[Encoding.UTF8.GetMaxByteCount(value.Length)];
        valueBytes = valueBytes[..Encoding.UTF8.GetBytes(value, valueBytes)];

        Add(fieldBytes, valueBytes);
    }

    public void AddAll(IEnumerable<KeyValuePair<string, string>> fields)
    {
        foreach (var (field, value) in fields)
            Add(field, value);
    }

    public readonly Enumerator GetEnumerator() => new(Bytes);

    private Span<byte> PrepareWrite(int length)
    {
        if (_length + length > _buffer.Length)
        {
            var newSize = Math.Max(_buffer.Length * 2, _length + length);
            _rentedBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            _buffer.CopyTo(_rentedBuffer);
            _buffer = _rentedBuffer;
        }
        var oldPosition = _length;
        _length += length;
        return _buffer[oldPosition.._length];
    }

    private void Write(scoped ReadOnlySpan<byte> value) => value.CopyTo(PrepareWrite(value.Length));
    private void Write(byte value) => Write([value]);
    private void Write(char value) => Write((byte)value);

    public ref struct Enumerator
    {
        private ReadOnlySpan<byte> _remaining;

        public FieldValuePair Current { get; private set; }

        internal Enumerator(ReadOnlySpan<byte> messageBytes)
        {
            _remaining = messageBytes;
            Current = default;
        }

        public bool MoveNext()
        {
            if (_remaining.IsEmpty)
                return false;

            var fieldEnd = _remaining.IndexOfAny(FieldSeparatorChars);
            if (fieldEnd == -1)
                throw new FormatException("Invalid message format: missing '=' or newline separator");

            var field = _remaining[..fieldEnd];
            var separator = _remaining[fieldEnd];
            _remaining = _remaining[(fieldEnd + 1)..];


            int valueEnd;
            if (separator == (byte)'=')
            {
                valueEnd = _remaining.IndexOf((byte)'\n');
                if (valueEnd == -1)
                    throw new FormatException("Invalid message format: missing newline after value");
                
            }
            else
            {
                valueEnd = (int)MemoryMarshal.Read<ulong>(_remaining);
                _remaining = _remaining[sizeof(ulong)..];
            }

            var value = _remaining[..valueEnd];

            if (_remaining[valueEnd] != (byte)'\n')
                throw new FormatException("Invalid message format: missing newline after value");
            
            _remaining = _remaining[(valueEnd + 1)..];
                
            Current = new FieldValuePair(field, value);
            return true;
        }
    }

    public readonly ref struct FieldValuePair
    {
        private readonly ReadOnlySpan<byte> _field;
        private readonly ReadOnlySpan<byte> _value;

        internal FieldValuePair(ReadOnlySpan<byte> field, ReadOnlySpan<byte> value)
        {
            _field = field;
            _value = value;
        }

        public ReadOnlySpan<byte> FieldBytes => _field;
        public ReadOnlySpan<byte> ValueBytes => _value;

        public string Field => Encoding.ASCII.GetString(_field);
        public string Value => Encoding.UTF8.GetString(_value);

        public void Deconstruct(out string field, out string value)
        {
            field = Field;
            value = Value;
        }
    }
}