using App.Enums;

namespace App.Models.udp;

public class UdpByeModel: ByeModel
{
    public UdpMessageType MessageType { get; } = UdpMessageType.Bye;
}