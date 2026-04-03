using System;

using LinuxCore;

using static SystemJournalCore.Interop.LibSystemD;

namespace SystemJournalCore;

public sealed unsafe class JournalReader : NativeObject
{
    private readonly sd_journal* _journal;

    public JournalReader(JournalType type = JournalType.All, DateTime? startTimestamp = null)
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
        var seekTimeNanoseconds = (ulong)((startTimestamp ?? DateTime.UtcNow) - DateTime.UnixEpoch).TotalNanoseconds;
        sd_journal_seek_realtime_usec(_journal,  seekTimeNanoseconds).ThrowIfError();
    }

    protected override void ReleaseUnmanagedResources()
    {
        if (_journal != null)
            sd_journal_close(_journal);
    }
}