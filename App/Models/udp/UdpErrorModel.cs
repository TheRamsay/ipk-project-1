using App.Enums;

namespace App.Models.udp;

public class UdpErrorModel: ErrorModel
{
    public UdpMessageType MessageType { get; } = UdpMessageType.Err;
}