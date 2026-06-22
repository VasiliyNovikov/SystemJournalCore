using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SystemJournalCore;

internal static class FieldCache
{
    private static readonly ASCIIEncoding Encoding = new();

    [ThreadStatic]
    private static Dictionary<string, CacheEntry>? _cache;

    [ThreadStatic]
    private static Dictionary<string, CacheEntry>.AlternateLookup<ReadOnlySpan<byte>>? _reverseCache;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> Get(string field) => GetEntry(field).Bytes;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Get(ReadOnlySpan<byte> field) => GetEntry(field).Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> GetNullTerminated(string field) => GetEntry(field).NullTerminatedBytes;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> GetNullTerminated(ReadOnlySpan<byte> field) => GetEntry(field).NullTerminatedBytes;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CacheEntry GetEntry(string field) => CollectionsMarshal.GetValueRefOrAddDefault(_cache ??= new(AsciiStringComparer.Instance), field, out _) ??= new(field);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CacheEntry GetEntry(ReadOnlySpan<byte> field)
    {
        var reverseCache = _reverseCache ??= (_cache ??= new(AsciiStringComparer.Instance)).GetAlternateLookup<ReadOnlySpan<byte>>();
        return CollectionsMarshal.GetValueRefOrAddDefault(reverseCache, field, out _) ??= new(field);
    }

    private class AsciiStringComparer : IEqualityComparer<string>, IAlternateEqualityComparer<ReadOnlySpan<byte>, string>
    {
        public static readonly AsciiStringComparer Instance = new();

        public bool Equals(string? x, string? y) => ReferenceEquals(x, y) || x is not null && x.Equals(y, StringComparison.Ordinal);

        [SkipLocalsInit]
        public bool Equals(ReadOnlySpan<byte> alternate, string other)
        {
            Span<byte> buffer = stackalloc byte[other.Length];
            Encoding.GetBytes(other, buffer);
            return alternate.SequenceEqual(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(ReadOnlySpan<byte> alternate)
        {
            HashCode hash = new();
            hash.AddBytes(alternate);
            return hash.ToHashCode();
        }

        [SkipLocalsInit]
        public int GetHashCode(string obj)
        {
            Span<byte> buffer = stackalloc byte[obj.Length];
            Encoding.GetBytes(obj, buffer);
            return GetHashCode(buffer);
        }

        public string Create(ReadOnlySpan<byte> alternate) => Encoding.GetString(alternate);
    }

    private class CacheEntry
    {
        private readonly byte[] _buffer;

        public string Value { get; }

        public ReadOnlySpan<byte> Bytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.AsSpan()[..^1];
        }

        public ReadOnlySpan<byte> NullTerminatedBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CacheEntry(string value)
        {
            Value = value;
            _buffer = new byte[value.Length + 1];
            Encoding.GetBytes(value, _buffer);
            _buffer[^1] = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CacheEntry(ReadOnlySpan<byte> value)
        {
            Value = Encoding.GetString(value);
            _buffer = new byte[value.Length + 1];
            value.CopyTo(_buffer);
            _buffer[^1] = 0;
        }
    }
}