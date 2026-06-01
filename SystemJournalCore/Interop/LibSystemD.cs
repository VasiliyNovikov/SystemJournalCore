using System;
using System.Runtime.InteropServices;

using LinuxCore;

namespace SystemJournalCore.Interop;

internal static unsafe partial class LibSystemD
{
    private const string LibraryName = "libsystemd.so";

    [Flags]
    public enum sd_journal_flags 
    {
        SD_JOURNAL_LOCAL_ONLY                = 1 << 0,
        SD_JOURNAL_RUNTIME_ONLY              = 1 << 1,
        SD_JOURNAL_SYSTEM                    = 1 << 2,
        SD_JOURNAL_CURRENT_USER              = 1 << 3,
        SD_JOURNAL_OS_ROOT                   = 1 << 4,
        SD_JOURNAL_ALL_NAMESPACES            = 1 << 5, /* Show all namespaces, not just the default or specified one */
        SD_JOURNAL_INCLUDE_DEFAULT_NAMESPACE = 1 << 6, /* Show default namespace in addition to specified one */
        SD_JOURNAL_TAKE_DIRECTORY_FD         = 1 << 7, /* sd_journal_open_directory_fd() will take ownership of the provided file descriptor. */
        SD_JOURNAL_ASSUME_IMMUTABLE          = 1 << 8, /* Assume the opened journal files are immutable. Journal entries added later may be ignored. */
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct sd_journal;

    // int sd_journal_open(sd_journal **ret, int flags);
    [LibraryImport(LibraryName, EntryPoint = "sd_journal_open")]
    public static partial JournalResult sd_journal_open(out sd_journal* journal, sd_journal_flags flags);

    // void sd_journal_close(sd_journal *j);
    [LibraryImport(LibraryName, EntryPoint = "sd_journal_close")]
    public static partial void sd_journal_close(sd_journal* journal);

    // int sd_journal_get_fd(sd_journal *j);
    [LibraryImport(LibraryName, EntryPoint = "sd_journal_get_fd")]
    public static partial JournalResult<FileDescriptor> sd_journal_get_fd(sd_journal* journal);

    // int sd_journal_process(sd_journal *j);
    [LibraryImport(LibraryName, EntryPoint = "sd_journal_process")]
    public static partial JournalResult<int> sd_journal_process(sd_journal* journal);

    // int sd_journal_seek_realtime_usec(sd_journal *j, uint64_t usec);
    [LibraryImport(LibraryName, EntryPoint = "sd_journal_seek_realtime_usec")]
    public static partial JournalResult sd_journal_seek_realtime_usec(sd_journal* journal, ulong usec);

    // int sd_journal_next(sd_journal *j);
    [LibraryImport(LibraryName, EntryPoint = "sd_journal_next")]
    public static partial JournalResult<int> sd_journal_next(sd_journal* journal);

    //  int sd_journal_enumerate_data(sd_journal *j, const void **data, size_t *length);
    [LibraryImport(LibraryName, EntryPoint = "sd_journal_enumerate_data")]
    public static partial JournalResult<int> sd_journal_enumerate_data(sd_journal* journal, out void* data, out nuint length);

    // int sd_journal_set_data_threshold(sd_journal *j, size_t sz);
    [LibraryImport(LibraryName, EntryPoint = "sd_journal_set_data_threshold")]
    public static partial JournalResult sd_journal_set_data_threshold(sd_journal* journal, nuint sz);

    // int sd_journal_add_match(sd_journal *j, const void *data, size_t size);
    [LibraryImport(LibraryName, EntryPoint = "sd_journal_add_match")]
    public static partial JournalResult sd_journal_add_match(sd_journal* journal, void* data, nuint size);

    // int sd_journal_add_disjunction(sd_journal *j);
    [LibraryImport(LibraryName, EntryPoint = "sd_journal_add_disjunction")]
    public static partial JournalResult sd_journal_add_disjunction(sd_journal* journal);

    // int sd_journal_add_conjunction(sd_journal *j);
    [LibraryImport(LibraryName, EntryPoint = "sd_journal_add_conjunction")]
    public static partial JournalResult sd_journal_add_conjunction(sd_journal* journal);

    // int sd_journal_get_realtime_usec(sd_journal *j, uint64_t *usec);
    [LibraryImport(LibraryName, EntryPoint = "sd_journal_get_realtime_usec")]
    public static partial JournalResult sd_journal_get_realtime_usec(sd_journal* journal, out ulong usec);
}