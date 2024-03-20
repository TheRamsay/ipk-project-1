namespace App.input;

public class StandardInputReader: IStandardInputReader
{
    public string? ReadLine()
    {
        return Console.ReadLine();
    }
}