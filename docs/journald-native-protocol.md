# journald native protocol

This is the lowest-level write protocol `SystemJournalCore` can wrap directly without going through `libsystemd`. It is the native journal protocol spoken by `systemd-journald` over a Unix domain datagram socket.

## Transport

- Socket type: `AF_UNIX` + `SOCK_DGRAM`
- Default endpoint: `/run/systemd/journal/socket`
- One datagram maps to one journal entry

This is the transport used by systemd's native journal client APIs behind `sd_journal_send()` / `sd_journal_sendv()`.

## Entry encoding

Each journal entry is serialized as a sequence of environment-like fields.

- Field names must be non-empty ASCII and must not contain control characters, `=`, or newline characters.
- Values may contain arbitrary bytes, including `NULL`.
- Fields whose names start with `_` are trusted fields added by journald; clients should not send them.

There are two wire encodings for a field:

1. `KEY=value\n`

   Use this when the value does not contain a newline byte.

2. `KEY\n<little-endian u64 length><raw bytes>\n`

   This form is required when the value contains a newline byte. It also works for binary payloads.

In practice, `MESSAGE` is the most important field. Common companion fields are `PRIORITY`, `SYSLOG_IDENTIFIER`, `CODE_FILE`, `CODE_LINE`, `CODE_FUNC`, and `ERRNO`.

## Large entries

If the encoded entry does not fit in a datagram, the client should retry using a sealed `memfd`:

- send an empty datagram
- attach exactly one `memfd` file descriptor
- put the same serialized entry bytes into that `memfd`

According to systemd's protocol description, other combinations are invalid and ignored.

## Reading and querying

The datagram protocol above is only for submitting entries. `journald` does not expose a symmetric native socket protocol for arbitrary reads or queries. On the read side, systemd's supported API surface is the `sd_journal_*` family in `libsystemd`, and `journalctl` is the CLI built on the same journal-file reader.

A typical read/query flow looks like this:

1. Open a journal view with `sd_journal_open()`, `sd_journal_open_directory()`, `sd_journal_open_files()`, or `sd_journal_open_namespace()`.
2. Add filters with `sd_journal_add_match()`. The matching model is the same one `journalctl` uses:
   - different fields are combined as `AND`
   - repeated matches for the same field are combined as `OR`
   - `sd_journal_add_disjunction()` / `sd_journal_add_conjunction()` build larger expressions
3. Seek with `sd_journal_seek_head()`, `sd_journal_seek_tail()`, `sd_journal_seek_realtime_usec()`, `sd_journal_seek_monotonic_usec()`, or `sd_journal_seek_cursor()`.
4. Step onto readable entries with `sd_journal_next()` / `sd_journal_previous()` and iterate from there.
5. Read data from the current entry with `sd_journal_get_data()` or `sd_journal_enumerate_data()`. Returned buffers are `FIELD=value` byte slices backed by a memory map.
6. Use cursors and change notifications for incremental readers: `sd_journal_get_cursor()`, `sd_journal_get_fd()`, `sd_journal_process()`, and `sd_journal_wait()`.

Two practical details matter for a wrapper API:

- read-side filters commonly target trusted fields such as `_SYSTEMD_UNIT` and `_BOOT_ID`, even though clients must not submit `_`-prefixed fields on the write path
- `sd_journal_get_data()` returns buffers prefixed with `FIELD=` rather than raw values, and large fields may be truncated unless `sd_journal_set_data_threshold(0)` is used

For higher-level queries, `sd_journal_query_unique()` can enumerate distinct values for a field. `journalctl` exposes the same match model from the command line with `FIELD=value`, `+`, `--since`, `--until`, `--cursor`, `--boot`, `--unit`, and related options.

## Notes for SystemJournalCore

The first implementation will likely need separate internal layers for write and read paths.

Write path:

- a field encoder for the native journal format
- a Unix datagram sender targeting `/run/systemd/journal/socket`
- a fallback path for oversized entries using `memfd`

Read/query path:

- a small `libsystemd` interop layer over `sd_journal_*`
- translation from `FIELD=value` buffers into public entry/query types
- cursor/follow handling for incremental readers

The public API can stay higher level than either underlying implementation. The write path should preserve journald's field rules and avoid exposing trusted `_` fields as user-settable values. The read path should expose filters, seeking, cursors, and follow/tail behavior without leaking `sd_journal*` handles into the public surface.

For testing and behavioral comparison, `journalctl` can still be useful as an external validation tool, but it should not be part of the runtime implementation path.

A fully managed read implementation is a larger project than the native write transport: it means parsing journal files on disk rather than speaking another socket protocol.

## Official references

- Native protocol spec: <https://systemd.io/JOURNAL_NATIVE_PROTOCOL/>
- Journal service overview: <https://www.freedesktop.org/software/systemd/man/latest/systemd-journald.service.html>
- Standard journal fields: <https://www.freedesktop.org/software/systemd/man/latest/systemd.journal-fields.html>
- Client API built on this protocol: <https://www.freedesktop.org/software/systemd/man/latest/sd_journal_sendv.html>
- Read/query API overview: <https://www.freedesktop.org/software/systemd/man/latest/sd-journal.html>
- Opening journals for reading: <https://www.freedesktop.org/software/systemd/man/latest/sd_journal_open.html>
- Match filtering: <https://www.freedesktop.org/software/systemd/man/latest/sd_journal_add_match.html>
- Seeking and iteration: <https://www.freedesktop.org/software/systemd/man/latest/sd_journal_seek_head.html>
- Reading fields: <https://www.freedesktop.org/software/systemd/man/latest/sd_journal_get_data.html>
- Change notifications and follow mode: <https://www.freedesktop.org/software/systemd/man/latest/sd_journal_get_fd.html>
- Querying unique field values: <https://www.freedesktop.org/software/systemd/man/latest/sd_journal_query_unique.html>
- CLI reader: <https://www.freedesktop.org/software/systemd/man/latest/journalctl.html>
