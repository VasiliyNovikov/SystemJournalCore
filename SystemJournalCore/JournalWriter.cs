using System;
using System.Text;

using LinuxCore;

namespace SystemJournalCore;

public sealed class JournalWriter : IDisposable
{
    private readonly UnixSocket _socket;
    
    public JournalWriter()
    {
        _socket = new UnixSocket(LinuxSocketType.Datagram);
        _socket.Connect(UnixSocketAddress.FromPath("/run/systemd/journal/socket"));
    }
    public void Dispose() => _socket.Dispose();

    public void Write(string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        _socket.Send(data);
    }
}