using App.Enums;

namespace App.Models.udp;

public class UdpMessageModel
{
    public UdpMessageType MessageType { get; set; } = UdpMessageType.Msg;
}