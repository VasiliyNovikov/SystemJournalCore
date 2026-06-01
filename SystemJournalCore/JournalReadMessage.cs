using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace SystemJournalCore;

public readonly ref struct JournalReadMessage(ReadOnlySpan<byte> bytes)
{
    private static readonly SearchValues<byte> FieldSeparatorChars = SearchValues.Create("=\n"u8);

    private readonly ReadOnlySpan<byte> _bytes = bytes;

    public Enumerator GetEnumerator() => new(_bytes);

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
                if (_remaining.Length < sizeof(ulong))
                    throw new FormatException("Invalid message format: missing binary value length");

                var valueLength = MemoryMarshal.Read<ulong>(_remaining);
                if (valueLength > int.MaxValue)
                    throw new FormatException("Invalid message format: binary value length is too large");

                valueEnd = (int)valueLength;
                _remaining = _remaining[sizeof(ulong)..];

                if (valueEnd >= _remaining.Length)
                    throw new FormatException("Invalid message format: binary value is truncated");
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