using System;
using System.Runtime.CompilerServices;

namespace SystemJournalCore;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly ref struct KeepAliveScope(object target)
{
    private readonly object? _target = target;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => GC.KeepAlive(_target);
}