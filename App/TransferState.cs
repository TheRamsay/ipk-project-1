using System.Net.Sockets;
using IPK.Project1.App.Enums;

namespace IPK.Project1.App;

public class TransferState
{
    public ProtocolState State { get; set; } = ProtocolState.Start;
    public required string DisplayName { get; set; }
    public required TcpClient Client {get; set; }
    public required CancellationTokenSource CancellationTokenSource { get; set; }
}