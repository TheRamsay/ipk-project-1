using App.Enums;

namespace App.Models.udp;

public class UdpConfirmModel: IBaseModel
{
   public UdpMessageType MessageType { get; set; } = UdpMessageType.Confirm;
    public short RefMessageId { get; set; }
}