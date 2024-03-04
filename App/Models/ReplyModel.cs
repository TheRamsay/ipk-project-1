using App.Enums;

namespace App.Models;

public class ReplyModel : IBaseModel
{
    public readonly UdpMessageType UdpMessageType = UdpMessageType.Reply;
    public required bool Status { get; set; }
    public required string Content { get; set; }
}