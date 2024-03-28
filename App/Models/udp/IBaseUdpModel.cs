using System.Text;
using App.Enums;

namespace App.Models.udp;

public interface IBaseUdpModel: IBaseModel
{
    public UdpMessageType MessageType { get; set; }
    public static byte[] Serialize(IBaseUdpModel model)
    {
        var properties = model.GetType().GetProperties();

        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(model);
            var propertyType = property.PropertyType;

            if (propertyType == typeof(UdpMessageType))
            {
                binaryWriter.Write((byte)propertyValue);
            } 
            else if (propertyType == typeof(string))
            {
                var strData = Encoding.ASCII.GetBytes((string)propertyValue);
                binaryWriter.Write(strData);
                binaryWriter.Write((byte)0);
            }
            else if (propertyType == typeof(int))
            {
                binaryWriter.Write((int)propertyValue);
            }
            else if (propertyType == typeof(short))
            {
                binaryWriter.Write((short)propertyValue);
            }
            else
            {
                throw new NotImplementedException("Property type not implemented");
            }

        }

        return memoryStream.ToArray();
    }
    
    public static IBaseUdpModel Deserialize(byte[] data)
    {
        var messageTypeToModel = new Dictionary<UdpMessageType, Type>
        {
            {UdpMessageType.Auth, typeof(UdpAuthModel)},
            {UdpMessageType.Confirm, typeof(UdpConfirmModel)},
            {UdpMessageType.Join, typeof(UdpJoinModel)},
            {UdpMessageType.Msg, typeof(UdpMessageModel)},
            {UdpMessageType.Reply, typeof(UdpReplyModel)},
            {UdpMessageType.Err, typeof(UdpErrorModel)},
            {UdpMessageType.Bye, typeof(UdpByeModel)}
        };

        Type modelType = messageTypeToModel[(UdpMessageType)data[0]];
        var model = (IBaseUdpModel)Activator.CreateInstance(modelType);
        
        using var memoryStream = new MemoryStream(data);
        using var binaryReader = new BinaryReader(memoryStream);
        
        var properties = modelType.GetProperties();

        foreach (var property in properties)
        {

            Type propertyType = property.PropertyType;
            
            if (propertyType == typeof(UdpMessageType))
            {
                property.SetValue(model, (UdpMessageType)binaryReader.ReadByte());
            } 
            else if (propertyType == typeof(string))
            {
                var byteList = new List<byte>();

                while (true)
                {
                    var byteValue = binaryReader.ReadByte();
                    if (byteValue == 0)
                    {
                        break;
                    }
                    byteList.Add(byteValue);
                }

                var strValue = Encoding.ASCII.GetString(byteList.ToArray());
                property.SetValue(model, strValue);
            } 
            else if (propertyType == typeof(int))
            {
                property.SetValue(model, binaryReader.ReadInt32());
            } 
            else if (propertyType == typeof(short))
            {
                property.SetValue(model, binaryReader.ReadInt16());
            } else if (propertyType == typeof(bool))
            {
                property.SetValue(model, binaryReader.ReadBoolean());
            }
        }

        return model;
    }
}