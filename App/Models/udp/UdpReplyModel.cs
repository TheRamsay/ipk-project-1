using App.Enums;

namespace App.Models.udp;

public class UdpReplyModel: ReplyModel
{
    public UdpMessageType MessageType { get; set; } = UdpMessageType.Reply;
    public short RefMessageId { get; set; }
}