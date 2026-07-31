using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SystemJournalCore.Tests;

[TestClass]
public class JournalControlTests
{
    [TestMethod]
    public void JournalControl_Write_Then_Read_RoundTrips()
    {
        var identifier = $"sjc-test-{Guid.NewGuid():N}";
        const string message = "hello from JournalControl test";

        JournalControl.Write(new Dictionary<string, string>
        {
            ["MESSAGE"] = message,
            ["SYSLOG_IDENTIFIER"] = identifier
        });

        var entries = JournalControl.Read(identifier: identifier, lines: 1).ToList();
        Assert.HasCount(1, entries);

        var entry = entries[0];
        Assert.AreEqual(message, entry["MESSAGE"]);
        Assert.AreEqual(identifier, entry["SYSLOG_IDENTIFIER"]);
    }
}