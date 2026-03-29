using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using LinuxCore;

namespace SystemJournalCore;

public sealed class JournalWriter : IDisposable
{
    private const string SocketPath = "/run/systemd/journal/socket";
    private readonly UnixSocket _socket;
    
    public JournalWriter()
    {
        _socket = new UnixSocket(LinuxSocketType.Datagram);
        _socket.Connect(UnixSocketAddress.FromPath(SocketPath));
    }

    public void Dispose() => _socket.Dispose();

    public void Write(JournalMessageWriter message)
    {
        try
        {
            _socket.Send(message.Bytes);
        }
        catch (LinuxException e) when (e.ErrorNumber == LinuxErrorNumber.MessageTooLong)
        {
            using var mem = new LinuxMemoryFile("journal", LinuxMemoryFileFlags.AllowSealing);
            mem.Write(message.Bytes);
            mem.AddSeals(LinuxMemoryFileSeals.Shrink | LinuxMemoryFileSeals.Grow | LinuxMemoryFileSeals.Write | LinuxMemoryFileSeals.Seal);
            _socket.SendFileDescriptors([], [mem.Descriptor]);
        }
    }

    [SkipLocalsInit]
    public void Write(Dictionary<string, string> message, int bufferSize = 1024)
    {
        using var writer = new JournalMessageWriter(stackalloc byte[bufferSize]);
        writer.AppendAll(message);
        Write(writer);
    }
}