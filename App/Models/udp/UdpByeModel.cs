using App.Enums;

namespace App.Models.udp;

public class UdpByeModel: ByeModel
{
    public UdpMessageType MessageType { get; set;  } = UdpMessageType.Bye;

    public UdpByeModel()
    {
    }
}