using System;
using System.Collections.Generic;
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
        var since = DateTime.UtcNow;

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
        const string expectedGroup = "expected";
        const string expectedMessage = "expected JournalReader filter message";
        var since = DateTime.UtcNow;

        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = "ignored JournalReader filter message",
            ["SYSLOG_IDENTIFIER"] = identifier,
            ["TEST_READER_GROUP"] = ignoredGroup
        });
        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = expectedMessage,
            ["SYSLOG_IDENTIFIER"] = identifier,
            ["TEST_READER_GROUP"] = expectedGroup
        });

        Thread.Sleep(JournalFlushDelay);

        using var reader = new JournalReader();
        reader.AddMatch("SYSLOG_IDENTIFIER", identifier);
        reader.AddMatch("TEST_READER_GROUP"u8, "expected"u8);
        reader.Seek(since);

        Assert.IsTrue(reader.Read(out var entry), "The expected journal entry was not readable.");
        Assert.IsTrue(entry.TryGetValue("SYSLOG_IDENTIFIER"u8, out var currentIdentifier));
        Assert.AreEqual(identifier, currentIdentifier);
        Assert.IsTrue(entry.TryGetValue("TEST_READER_GROUP"u8, out var group));
        Assert.AreEqual(expectedGroup, group, "JournalReader returned an entry from the wrong TEST_READER_GROUP.");
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
}
