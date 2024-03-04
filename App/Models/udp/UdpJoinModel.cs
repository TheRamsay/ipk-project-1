using App.Enums;

namespace App.Models.udp;

public class UdpJoinModel: JoinModel
{
    public UdpMessageType MessageType { get; } = UdpMessageType.Join;
}