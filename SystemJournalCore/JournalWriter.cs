using System;

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

    public void Write(JournalWriteMessage message)
    {
        try
        {
            _socket.Send(message.Bytes);
        }
        catch (LinuxException e) when (e.ErrorNumber is LinuxErrorNumber.TryAgain or LinuxErrorNumber.MessageTooLong or LinuxErrorNumber.NoBufferSpaceAvailable)
        {
            using var mem = new LinuxMemoryFile("journal", LinuxMemoryFileFlags.AllowSealing);
            mem.Write(message.Bytes);
            mem.AddSeals(LinuxMemoryFileSeals.Shrink | LinuxMemoryFileSeals.Grow | LinuxMemoryFileSeals.Write | LinuxMemoryFileSeals.Seal);
            _socket.SendFileDescriptors([], [mem.Descriptor]);
        }
    }
}