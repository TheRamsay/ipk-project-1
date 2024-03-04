using System.Text;
using App.Enums;

namespace App.Models.udp;

public class UdpAuthModel: AuthModel, IBaseUdpModel
{
    public UdpMessageType UdpMessageType { get; set; } = UdpMessageType.Auth;
    
    
}