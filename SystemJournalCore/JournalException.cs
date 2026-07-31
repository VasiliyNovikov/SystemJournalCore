using LinuxCore;

namespace SystemJournalCore;

public sealed class JournalException(LinuxErrorNumber errorNumber) : LinuxException(errorNumber);