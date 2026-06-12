using System;
using System.Text;

namespace SystemJournalCore;

internal static class ValueEncoder
{
    private static readonly UTF8Encoding Encoding = new(false);

    public static string Get(ReadOnlySpan<byte> value) => Encoding.GetString(value);
    public static int EstimateByteCount(string value) => Encoding.GetMaxByteCount(value.Length);
    public static int GetBytes(string value, Span<byte> valueBytes) => Encoding.GetBytes(value, valueBytes);
    public static byte[] GetBytes(string value) => Encoding.GetBytes(value);
}