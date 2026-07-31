using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using LinuxCore;

namespace SystemJournalCore.Interop;

[StructLayout(LayoutKind.Sequential)]
public readonly struct JournalResult
{
    private readonly int _result;

    public LinuxErrorNumber ErrorNumber
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _result < 0 ? (LinuxErrorNumber)(-_result) : LinuxErrorNumber.OK;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ThrowIfError()
    {
        var errorNumber = ErrorNumber;
        if (errorNumber != LinuxErrorNumber.OK)
            throw new JournalException(errorNumber);
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly unsafe struct JournalResult<T> where T : unmanaged
{
    private readonly T _value;

    public LinuxErrorNumber ErrorNumber
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            switch (sizeof(T))
            {
                case 4:
                {
                    var result = Unsafe.BitCast<T, int>(_value);
                    return result < 0 ? (LinuxErrorNumber)(-result) : LinuxErrorNumber.OK;
                }
                case 8:
                {
                    var result = Unsafe.BitCast<T, long>(_value);
                    return result < 0 ? (LinuxErrorNumber)(-result) : LinuxErrorNumber.OK;
                }
                default:
                    throw new NotSupportedException($"JournalResult<{typeof(T).Name}> with size {sizeof(T)} is not supported");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ThrowIfError()
    {
        var errorNumber = ErrorNumber;
        return errorNumber == LinuxErrorNumber.OK ? _value : throw new JournalException(errorNumber);
    }
}