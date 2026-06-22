using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using LinuxCore;

using static SystemJournalCore.Interop.LibSystemD;

namespace SystemJournalCore;

public readonly unsafe struct JournalEntryReader
{
    private readonly sd_journal* _journal;

    public ReadOnlySpan<byte> this[ReadOnlySpan<byte> field]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetValueBytesImpl(FieldCache.GetNullTerminated(field));
    }

    public ReadOnlySpan<byte> this[string field]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetValueBytesImpl(FieldCache.GetNullTerminated(field));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JournalEntryReader(sd_journal* journal) => _journal = journal;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValueBytes(ReadOnlySpan<byte> field, out ReadOnlySpan<byte> valueBytes) => TryGetValueBytesImpl(FieldCache.GetNullTerminated(field), out valueBytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(ReadOnlySpan<byte> field, [MaybeNullWhen(false)]out string value)
    {
        if (TryGetValueBytesImpl(FieldCache.GetNullTerminated(field), out var valueBytes))
        {
            value = ValueEncoder.Get(valueBytes);
            return true;
        }
        value = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetValue(ReadOnlySpan<byte> field) => ValueEncoder.Get(GetValueBytesImpl(FieldCache.GetNullTerminated(field)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetValue(string field) => ValueEncoder.Get(GetValueBytesImpl(FieldCache.GetNullTerminated(field)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_journal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetValueBytesImpl(ReadOnlySpan<byte> nullTerminatedField, out ReadOnlySpan<byte> valueBytes)
    {
        var result = sd_journal_get_data(_journal, nullTerminatedField, out var dataPtr, out var dataLength);
        switch (result.ErrorNumber)
        {
            case LinuxErrorNumber.OK:
                valueBytes = new FieldValuePair(dataPtr, dataLength).ValueBytes;
                return true;
            case LinuxErrorNumber.NoSuchFileOrDirectory:
                valueBytes = default;
                return false;
            default:
                throw new JournalException(result.ErrorNumber);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<byte> GetValueBytesImpl(ReadOnlySpan<byte> nullTerminatedField)
    {
        return TryGetValueBytesImpl(nullTerminatedField, out var valueBytes) ? valueBytes : throw new KeyNotFoundException($"The field '{FieldCache.Get(nullTerminatedField[..^1])}' was not found in the journal entry");
    }

    public ref struct Enumerator
    {
        private readonly sd_journal* _journal;

        public FieldValuePair Current { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(sd_journal* journal) => _journal = journal;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (sd_journal_enumerate_data(_journal, out var dataPtr, out var dataLength).ThrowIfError() == 0)
            {
                Current = default;
                return false;
            }
            Current = new FieldValuePair(dataPtr, dataLength);
            return true;
        }
    }

    public readonly ref struct FieldValuePair
    {
        public ReadOnlySpan<byte> FieldBytes { get; }
        public ReadOnlySpan<byte> ValueBytes { get; }

        public string Field
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => FieldCache.Get(FieldBytes);
        }

        public string Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ValueEncoder.Get(ValueBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal FieldValuePair(byte* dataPtr, nuint dataLength)
        {
            var data = new ReadOnlySpan<byte>(dataPtr, (int)dataLength);
            var fieldEnd = data.IndexOf((byte)'=');
            FieldBytes = data[..fieldEnd];
            ValueBytes = data[(fieldEnd + 1)..];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out string field, out string value)
        {
            field = Field;
            value = Value;
        }
    }
}