using App.Enums;

namespace App.Models.udp;

public class UdpJoinModel: IBaseUdpModel, IModelWithId
{
    public UdpMessageType MessageType { get; set;  } = UdpMessageType.Join;
    public short Id { get; set; }
    public string ChannelId { get; set; }
    public string DisplayName { get; set; }
    public UdpJoinModel()
    {
    }
}