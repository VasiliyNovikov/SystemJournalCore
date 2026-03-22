# journald native protocol

This is the lowest-level protocol `SystemJournalCore` can wrap directly without going through `libsystemd`. It is the native journal protocol spoken by `systemd-journald` over a Unix domain datagram socket.

## Transport

- Socket type: `AF_UNIX` + `SOCK_DGRAM`
- Default endpoint: `/run/systemd/journal/socket`
- One datagram maps to one journal entry

This is the transport used by systemd's native journal client APIs behind `sd_journal_send()` / `sd_journal_sendv()`.

## Entry encoding

Each journal entry is serialized as a sequence of environment-like fields.

- Field names must be non-empty ASCII and must not contain control characters, `=`, or newline characters.
- Values may contain arbitrary bytes, including `NUL`.
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

## Notes for SystemJournalCore

The first transport layer in this library will likely need three small internal pieces:

- a field encoder for the native journal format
- a Unix datagram sender targeting `/run/systemd/journal/socket`
- a fallback path for oversized entries using `memfd`

The public API can stay higher level than the wire format, but the implementation should preserve journald's field rules and avoid exposing trusted `_` fields as user-settable values.

## Official references

- Native protocol spec: <https://systemd.io/JOURNAL_NATIVE_PROTOCOL/>
- Journal service overview: <https://www.freedesktop.org/software/systemd/man/latest/systemd-journald.service.html>
- Standard journal fields: <https://www.freedesktop.org/software/systemd/man/latest/systemd.journal-fields.html>
- Client API built on this protocol: <https://www.freedesktop.org/software/systemd/man/latest/sd_journal_sendv.html>
