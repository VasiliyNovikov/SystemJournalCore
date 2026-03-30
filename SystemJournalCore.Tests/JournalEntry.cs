using System.Collections.Generic;
using System.Text;

namespace SystemJournalCore.Tests;

internal sealed class JournalEntry
{
    private readonly Dictionary<string, byte[]> _fields = [];

    public string this[string key] => Encoding.UTF8.GetString(_fields[key]);

    public byte[] Bytes(string key) => _fields[key];

    public void Add(string key, string value) => _fields.Add(key, Encoding.UTF8.GetBytes(value));
    public void Add(string key, byte[] value) => _fields.Add(key, value);
}