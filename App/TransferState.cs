using System.Net.Sockets;
using App.Enums;

namespace App;

public class TransferState
{
    public ProtocolState State { get; set; } = ProtocolState.Start;
    public required string DisplayName { get; set; }
    public required TcpClient Client {get; set; }
    public required CancellationTokenSource CancellationTokenSource { get; set; }
}