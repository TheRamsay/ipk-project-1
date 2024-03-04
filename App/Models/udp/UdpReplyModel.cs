using App.Enums;

namespace App.Models.udp;

public class UdpReplyModel: ReplyModel
{
    public UdpMessageType MessageType { get; } = UdpMessageType.Reply;
    public short RefMessageId { get; set; }
}