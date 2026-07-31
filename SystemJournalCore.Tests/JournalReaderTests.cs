using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SystemJournalCore.Tests;

[TestClass]
public class JournalReaderTests
{
    private static readonly TimeSpan JournalFlushDelay = TimeSpan.FromMilliseconds(10);

    [TestMethod]
    public void Read_WithIdentifierMatch_ReturnsExpectedEntry()
    {
        var identifier = $"sjc-reader-test-{Guid.NewGuid():N}";
        const string message = "hello from JournalReader test";
        const string customValue = "reader-value";
        var since = DateTimeOffset.UtcNow;

        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = message,
            ["SYSLOG_IDENTIFIER"] = identifier,
            ["TEST_READER_VALUE"] = customValue
        });

        Thread.Sleep(JournalFlushDelay);

        using var reader = new JournalReader();
        reader.AddMatch("SYSLOG_IDENTIFIER", identifier);
        reader.Seek(since);

        Assert.IsTrue(reader.Read(out var entry), "The expected journal entry was not readable.");
        Assert.IsTrue(entry.TryGetValue("SYSLOG_IDENTIFIER"u8, out var currentIdentifier));
        Assert.AreEqual(identifier, currentIdentifier);
        Assert.AreEqual(message, entry.GetValue("MESSAGE"));
        Assert.IsTrue(entry["MESSAGE"].SequenceEqual(ValueEncoder.GetBytes(message)));
        Assert.IsTrue(entry["TEST_READER_VALUE"u8].SequenceEqual(ValueEncoder.GetBytes(customValue)));
        Assert.IsTrue(entry.TryGetValue("TEST_READER_VALUE"u8, out var currentCustomValue));
        Assert.AreEqual(customValue, currentCustomValue);
        Assert.IsTrue(entry.TryGetValueBytes("TEST_READER_VALUE"u8, out var customValueBytes));
        Assert.IsTrue(customValueBytes.SequenceEqual(ValueEncoder.GetBytes(customValue)));
        Assert.IsFalse(entry.TryGetValue("TEST_READER_MISSING"u8, out var missingValue));
        Assert.IsNull(missingValue);
        Assert.IsFalse(entry.TryGetValueBytes("TEST_READER_MISSING"u8, out var missingValueBytes));
        Assert.IsTrue(missingValueBytes.IsEmpty);

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (field, value) in entry)
        {
            if (field is "MESSAGE" or "SYSLOG_IDENTIFIER" or "TEST_READER_VALUE")
                fields[field] = value;
        }

        Assert.IsTrue(fields.TryGetValue("MESSAGE", out var enumeratedMessage));
        Assert.AreEqual(message, enumeratedMessage);
        Assert.IsTrue(fields.TryGetValue("SYSLOG_IDENTIFIER", out var enumeratedIdentifier));
        Assert.AreEqual(identifier, enumeratedIdentifier);
        Assert.IsTrue(fields.TryGetValue("TEST_READER_VALUE", out var enumeratedCustomValue));
        Assert.AreEqual(customValue, enumeratedCustomValue);

        Assert.IsGreaterThan(0UL, reader.CurrentTimestampMicroseconds);
        var currentTimestamp = reader.CurrentTimestamp;
        Assert.IsTrue(currentTimestamp >= since.AddSeconds(-5), $"Entry timestamp {currentTimestamp:O} is before expected lower bound {since:O}.");
        Assert.IsTrue(currentTimestamp <= DateTime.UtcNow.AddSeconds(5), $"Entry timestamp {currentTimestamp:O} is after the current UTC time.");
    }

    [TestMethod]
    public void Read_WithMultipleMatches_ReturnsOnlyMatchingEntries()
    {
        var identifier = $"sjc-reader-filter-test-{Guid.NewGuid():N}";
        const string ignoredGroup = "ignored";
        var expectedGroup = new string('g', 300);
        var expectedKind = new string('k', 300);
        const string expectedMessage = "expected JournalReader filter message";
        var since = DateTimeOffset.UtcNow;

        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = "ignored JournalReader filter message",
            ["SYSLOG_IDENTIFIER"] = identifier,
            ["TEST_READER_GROUP"] = ignoredGroup,
            ["TEST_READER_KIND"] = ignoredGroup
        });
        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = expectedMessage,
            ["SYSLOG_IDENTIFIER"] = identifier,
            ["TEST_READER_GROUP"] = expectedGroup,
            ["TEST_READER_KIND"] = expectedKind
        });

        Thread.Sleep(JournalFlushDelay);

        using var reader = new JournalReader();
        reader.AddMatch("SYSLOG_IDENTIFIER", identifier);
        reader.AddMatch("TEST_READER_GROUP", expectedGroup);
        reader.AddMatch("TEST_READER_KIND"u8, ValueEncoder.GetBytes(expectedKind));
        reader.Seek(since);

        Assert.IsTrue(reader.Read(out var entry), "The expected journal entry was not readable.");
        Assert.IsTrue(entry.TryGetValue("SYSLOG_IDENTIFIER"u8, out var currentIdentifier));
        Assert.AreEqual(identifier, currentIdentifier);
        Assert.IsTrue(entry.TryGetValue("TEST_READER_GROUP"u8, out var group));
        Assert.AreEqual(expectedGroup, group, "JournalReader returned an entry from the wrong TEST_READER_GROUP.");
        Assert.IsTrue(entry.TryGetValue("TEST_READER_KIND"u8, out var kind));
        Assert.AreEqual(expectedKind, kind, "JournalReader returned an entry from the wrong TEST_READER_KIND.");
        Assert.AreEqual(expectedMessage, entry.GetValue("MESSAGE"));
    }

    [TestMethod]
    public void SeekHead_And_SeekTail_DoNotThrow()
    {
        using var reader = new JournalReader();
        reader.SeekHead();
        reader.SeekTail();
    }

    [TestMethod]
    public void DataThreshold_SetAndGet_RoundTrips()
    {
        using var reader = new JournalReader();

        const ulong threshold = 128;
        reader.DataThreshold = threshold;
        Assert.AreEqual(threshold, reader.DataThreshold);

        reader.DataThreshold = null;
        Assert.IsNull(reader.DataThreshold);
    }

    [TestMethod]
    public void Read_InvalidatesPreviousEntryAndEnumerator()
    {
        var identifier = $"sjc-reader-version-test-{Guid.NewGuid():N}";
        var since = DateTimeOffset.UtcNow;

        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = "first",
            ["SYSLOG_IDENTIFIER"] = identifier
        });
        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = "second",
            ["SYSLOG_IDENTIFIER"] = identifier
        });

        Thread.Sleep(JournalFlushDelay);

        using var reader = new JournalReader();
        reader.AddMatch("SYSLOG_IDENTIFIER", identifier);
        reader.Seek(since);

        Assert.IsTrue(reader.Read(out var previousEntry));
        var previousEnumerator = previousEntry.GetEnumerator();
        Assert.IsTrue(reader.Read(out var currentEntry));

        Assert.ThrowsExactly<InvalidOperationException>(() => previousEntry.GetValue("MESSAGE"));
        Assert.AreEqual("second", currentEntry.GetValue("MESSAGE"));
        try
        {
            previousEnumerator.MoveNext();
            Assert.Fail("The previous entry enumerator should be invalidated.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [TestMethod]
    public void SeekAndAddMatch_InvalidateCurrentEntry()
    {
        var identifier = $"sjc-reader-version-test-{Guid.NewGuid():N}";
        var since = DateTimeOffset.UtcNow;

        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = "version test",
            ["SYSLOG_IDENTIFIER"] = identifier
        });

        Thread.Sleep(JournalFlushDelay);

        using var seekReader = new JournalReader();
        seekReader.AddMatch("SYSLOG_IDENTIFIER", identifier);
        seekReader.Seek(since);
        Assert.IsTrue(seekReader.Read(out var seekEntry));

        seekReader.SeekTail();
        Assert.ThrowsExactly<InvalidOperationException>(() => seekEntry.GetValue("MESSAGE"));

        using var matchReader = new JournalReader();
        matchReader.Seek(since);
        Assert.IsTrue(matchReader.Read(out var matchEntry));

        matchReader.AddMatch("SYSLOG_IDENTIFIER", identifier);
        Assert.ThrowsExactly<InvalidOperationException>(() => matchEntry.GetValue("MESSAGE"));
    }

    [TestMethod]
    public void DataThreshold_DoesNotInvalidateCurrentEntry()
    {
        var identifier = $"sjc-reader-version-test-{Guid.NewGuid():N}";
        var since = DateTimeOffset.UtcNow;

        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = "threshold test",
            ["SYSLOG_IDENTIFIER"] = identifier
        });

        Thread.Sleep(JournalFlushDelay);

        using var reader = new JournalReader();
        reader.AddMatch("SYSLOG_IDENTIFIER", identifier);
        reader.Seek(since);
        Assert.IsTrue(reader.Read(out var entry));

        reader.DataThreshold = 128;

        Assert.AreEqual("threshold test", entry.GetValue("MESSAGE"));
    }

    [TestMethod]
    public void DisposedReader_InvalidatesCurrentEntry()
    {
        var identifier = $"sjc-reader-version-test-{Guid.NewGuid():N}";
        var since = DateTimeOffset.UtcNow;

        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = "dispose test",
            ["SYSLOG_IDENTIFIER"] = identifier
        });

        Thread.Sleep(JournalFlushDelay);

        var reader = new JournalReader();
        reader.AddMatch("SYSLOG_IDENTIFIER", identifier);
        reader.Seek(since);
        Assert.IsTrue(reader.Read(out var entry));

        reader.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => entry.GetValue("MESSAGE"));
    }

    [TestMethod]
    public void Read_EndOfJournalInvalidatesEntryAndReturnsDefaultEntry()
    {
        var identifier = $"sjc-reader-version-test-{Guid.NewGuid():N}";
        var since = DateTimeOffset.UtcNow;

        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = "only entry",
            ["SYSLOG_IDENTIFIER"] = identifier
        });

        Thread.Sleep(JournalFlushDelay);

        using var reader = new JournalReader();
        reader.AddMatch("SYSLOG_IDENTIFIER", identifier);
        reader.Seek(since);
        Assert.IsTrue(reader.Read(out var previousEntry));

        Assert.IsFalse(reader.Read(out var defaultEntry));

        Assert.ThrowsExactly<InvalidOperationException>(() => previousEntry.GetValue("MESSAGE"));
        Assert.ThrowsExactly<InvalidOperationException>(() => defaultEntry.GetValue("MESSAGE"));
    }

    [TestMethod]
    public void DisposedReader_TakesPrecedenceOverStaleEntry()
    {
        var identifier = $"sjc-reader-version-test-{Guid.NewGuid():N}";
        var since = DateTimeOffset.UtcNow;

        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = "dispose precedence test",
            ["SYSLOG_IDENTIFIER"] = identifier
        });

        Thread.Sleep(JournalFlushDelay);

        var reader = new JournalReader();
        reader.AddMatch("SYSLOG_IDENTIFIER", identifier);
        reader.Seek(since);
        Assert.IsTrue(reader.Read(out var entry));
        reader.SeekTail();
        reader.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => entry.GetValue("MESSAGE"));
    }

    [TestMethod]
    public void Entry_KeepsReaderAlive()
    {
        var identifier = $"sjc-reader-version-test-{Guid.NewGuid():N}";
        var since = DateTimeOffset.UtcNow;

        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = "lifetime test",
            ["SYSLOG_IDENTIFIER"] = identifier
        });

        Thread.Sleep(JournalFlushDelay);

        var (entry, readerReference) = ReadEntryWithoutRetainingReader(identifier, since);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.AreEqual("lifetime test", entry.GetValue("MESSAGE"));
        Assert.IsTrue(readerReference.TryGetTarget(out var reader));
        reader.Dispose();
    }

    [TestMethod]
    public void Read_DoesNotAllocate()
    {
        var identifier = $"sjc-reader-version-test-{Guid.NewGuid():N}";
        var since = DateTimeOffset.UtcNow;

        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = "allocation warmup",
            ["SYSLOG_IDENTIFIER"] = identifier
        });
        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = "allocation measured",
            ["SYSLOG_IDENTIFIER"] = identifier
        });

        Thread.Sleep(JournalFlushDelay);

        using var reader = new JournalReader();
        reader.AddMatch("SYSLOG_IDENTIFIER", identifier);
        reader.Seek(since);
        Assert.IsTrue(reader.Read(out _));

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var read = reader.Read(out var entry);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.IsTrue(read);
        Assert.AreEqual(0L, allocated);
        Assert.AreEqual("allocation measured", entry.GetValue("MESSAGE"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (JournalEntryReader Entry, WeakReference<JournalReader> ReaderReference) ReadEntryWithoutRetainingReader(string identifier, DateTimeOffset since)
    {
        var reader = new JournalReader();
        reader.AddMatch("SYSLOG_IDENTIFIER", identifier);
        reader.Seek(since);
        Assert.IsTrue(reader.Read(out var entry));
        return (entry, new WeakReference<JournalReader>(reader));
    }
}