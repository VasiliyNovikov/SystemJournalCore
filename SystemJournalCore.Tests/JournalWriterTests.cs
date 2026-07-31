using System;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SystemJournalCore.Tests;

[TestClass]
public class JournalWriterTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void Write_RoundTrips(bool longMessage, bool newlineInValue)
    {
        var identifier = $"sjc-test-{Guid.NewGuid():N}";
        var message = longMessage
            ? new string('x', 256 * 1024)
            : "short test message";
        var testData = newlineInValue
            ? "line1\nline2\nline3"
            : "single-line-value";
        byte[] binaryData = [0x00, 0xFF, 0x80, 0xFE, 0x01];

        using var writeMessage = new JournalWriteMessage(stackalloc byte[512]);
        writeMessage.Add("SYSLOG_IDENTIFIER"u8, identifier);
        writeMessage.Add("MESSAGE"u8, message);
        writeMessage.Add("TEST_DATA"u8, testData);
        writeMessage.Add("TEST_BINARY"u8, binaryData);

        using var writer = new JournalWriter();
        writer.Write(writeMessage);

        var entries = JournalControl.Read(identifier: identifier, lines: 1).ToList();
        Assert.HasCount(1, entries);

        var entry = entries[0];
        Assert.AreEqual(message, entry["MESSAGE"]);
        Assert.AreEqual(identifier, entry["SYSLOG_IDENTIFIER"]);
        Assert.AreEqual(testData, entry["TEST_DATA"]);
        CollectionAssert.AreEqual(binaryData, entry.Bytes("TEST_BINARY"));
    }
}