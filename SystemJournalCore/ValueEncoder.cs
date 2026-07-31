using System;
using System.Text;

namespace SystemJournalCore;

internal static class ValueEncoder
{
    private static readonly UTF8Encoding Encoding = new(false);

    public static string Get(ReadOnlySpan<byte> value) => Encoding.GetString(value);
    public static int GetByteCount(string value) => Encoding.GetByteCount(value);
    public static int GetBytes(string value, Span<byte> valueBytes) => Encoding.GetBytes(value, valueBytes);
    public static byte[] GetBytes(string value) => Encoding.GetBytes(value);
}