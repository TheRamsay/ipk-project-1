namespace App.Input;

public class StandardInputReader: IStandardInputReader
{
    public string? ReadLine()
    {
        return Console.ReadLine();
    }
}