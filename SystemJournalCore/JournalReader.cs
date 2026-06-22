using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using LinuxCore;

using static SystemJournalCore.Interop.LibSystemD;

namespace SystemJournalCore;

public sealed unsafe class JournalReader : NativeObject
{
    private const int StackallocByteThreshold = 512;

    private readonly sd_journal* _journal;

    public ulong CurrentTimestampMicroseconds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            sd_journal_get_realtime_usec(_journal, out var usec).ThrowIfError();
            return usec;
        }
    }

    public ulong? DataThreshold
    {
        get
        {
            sd_journal_get_data_threshold(_journal, out var sz).ThrowIfError();
            return sz == 0 ? null : sz;
        }
        set => sd_journal_set_data_threshold(_journal, (nuint)(value ?? 0)).ThrowIfError();
    }

    public DateTime CurrentTimestamp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => DateTime.UnixEpoch.AddTicks((long)(CurrentTimestampMicroseconds * TimeSpan.TicksPerMicrosecond));
    }

    public JournalReader(JournalType type = JournalType.All)
    {
        var flags = sd_journal_flags.SD_JOURNAL_LOCAL_ONLY;
        switch (type)
        {
            case JournalType.System:
                flags |= sd_journal_flags.SD_JOURNAL_SYSTEM;
                break;
            case JournalType.User:
                flags |= sd_journal_flags.SD_JOURNAL_CURRENT_USER;
                break;
            case JournalType.All:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
        sd_journal_open(out _journal, flags).ThrowIfError();
    }

    protected override void ReleaseUnmanagedResources()
    {
        if (_journal is not null)
            sd_journal_close(_journal);
    }

    public void SeekHead() => sd_journal_seek_head(_journal).ThrowIfError();
    public void SeekTail() => sd_journal_seek_tail(_journal).ThrowIfError();
    public void Seek(DateTime timestamp) => sd_journal_seek_realtime_usec(_journal, (ulong)(timestamp - DateTime.UnixEpoch).TotalMicroseconds).ThrowIfError();

    [SkipLocalsInit]
    public void AddMatch(ReadOnlySpan<byte> field, ReadOnlySpan<byte> value)
    {
        var length = field.Length + 1 + value.Length;
        byte[]? rentedBuffer = null;
        var data = length <= StackallocByteThreshold
            ? stackalloc byte[length]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            field.CopyTo(data);
            data[field.Length] = (byte)'=';
            value.CopyTo(data[(field.Length + 1)..]);

            fixed (byte* dataPtr = data)
                sd_journal_add_match(_journal, dataPtr, (nuint)data.Length).ThrowIfError();
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    [SkipLocalsInit]
    public void AddMatch(ReadOnlySpan<byte> field, string value)
    {
        var valueLength = ValueEncoder.GetByteCount(value);
        byte[]? rentedBuffer = null;
        var data = valueLength <= StackallocByteThreshold
            ? stackalloc byte[valueLength]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(valueLength)).AsSpan(0, valueLength);
        try
        {
            ValueEncoder.GetBytes(value, data);
            AddMatch(field, data);
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    public void AddMatch(string field, string value) => AddMatch(FieldCache.Get(field), value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Read(out JournalEntryReader entry)
    {
        if (sd_journal_next(_journal).ThrowIfError() == 0)
        {
            entry = default;
            return false;
        }
        entry = new JournalEntryReader(_journal);
        return true;
    }
}