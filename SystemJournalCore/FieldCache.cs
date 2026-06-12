using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SystemJournalCore;

internal static class FieldCache
{
    private static readonly ASCIIEncoding Encoding = new();

    [ThreadStatic]
    private static Dictionary<string, byte[]>? _cache;

    [ThreadStatic]
    private static Dictionary<byte[], string>.AlternateLookup<ReadOnlySpan<byte>>? _reverseCache;

    public static byte[] Get(string field)
    {
        var cache = _cache ??= [];
        return CollectionsMarshal.GetValueRefOrAddDefault(cache, field, out _) ??= Encoding.GetBytes(field);
    }

    public static string Get(ReadOnlySpan<byte> field)
    {
        var reverseCache = _reverseCache ??= new Dictionary<byte[], string>(ByteArrayComparer.Instance).GetAlternateLookup<ReadOnlySpan<byte>>();
        return CollectionsMarshal.GetValueRefOrAddDefault(reverseCache, field, out _) ??= Encoding.GetString(field);
    }

    private class ByteArrayComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
    {
        public static readonly ByteArrayComparer Instance = new();
        public bool Equals(byte[]? x, byte[]? y) => ReferenceEquals(x, y) || x is not null && y is not null && x.SequenceEqual(y);
        public bool Equals(ReadOnlySpan<byte> alternate, byte[] other) => alternate.SequenceEqual(other);

        public int GetHashCode(ReadOnlySpan<byte> alternate)
        {
            HashCode hash = new();
            hash.AddBytes(alternate);
            return hash.ToHashCode();
        }

        public int GetHashCode(byte[] obj) => GetHashCode(obj.AsSpan());

        public byte[] Create(ReadOnlySpan<byte> alternate) => alternate.ToArray();
    }
}