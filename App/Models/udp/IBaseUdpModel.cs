using System.Text;
using App.Enums;

namespace App.Models.udp;

public interface IBaseUdpModel: IBaseModel
{
    public static byte[] Serialize(UdpAuthModel model)
    {
        var properties = model.GetType().GetProperties();

        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(model);

            switch (propertyValue)
            {
                case int intValue:
                    binaryWriter.Write(intValue);
                    break;
                case short shortValue:
                    binaryWriter.Write(shortValue);
                    break;
                case string stringValue:
                {
                    var strData = Encoding.ASCII.GetBytes(stringValue);
                    binaryWriter.Write(strData);
                    binaryWriter.Write((byte)0);
                    break;
                }
                case UdpMessageModel udpMessageType:
                    binaryWriter.Write((byte)udpMessageType.MessageType);
                    break;
                default:
                    throw new NotImplementedException("Property type not implemented");
            }
        }

        return memoryStream.ToArray();
    }
    
    public static IBaseModel Deserialize(byte[] data)
    {
        using var memoryStream = new MemoryStream(data);
        using var binaryReader = new BinaryReader(memoryStream);
        
        var model = new UdpAuthModel
        {
            Username = null,
            DisplayName = null,
            Secret = null
        };
        
        var properties = model.GetType().GetProperties();

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
            }
        }

        return model;
    }
}