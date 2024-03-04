using App.Enums;

namespace App.Models.udp;

public class UdpMessageModel
{
    public UdpMessageType MessageType { get; } = UdpMessageType.Msg;
}