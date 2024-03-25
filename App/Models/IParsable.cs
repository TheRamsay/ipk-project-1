namespace App.Models;

public interface IParsable
{
    public static IParsable Parse(string data) => throw new NotImplementedException("Parse method not implemented on base interface");
}