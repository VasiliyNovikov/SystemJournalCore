# SystemJournalCore

.NET wrapper for the Linux system journal.

Runtime requires `libsystemd.so.0` (`libsystemd0` on Ubuntu/Debian).

This repository is currently a scaffold only. The implementation is still to be designed.

Current design notes:

- `docs/journald-native-protocol.md` - brief summary of the native journald socket protocol the library can wrap at the lowest level. GitHub copy: <https://github.com/VasiliyNovikov/SystemJournalCore/blob/main/docs/journald-native-protocol.md>
