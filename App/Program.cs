using System.Net.Sockets;
using System.Text;
using CommandLine;
using IPK.Project1.App.Enums;

namespace  IPK.Project1.App;

static class Program
{
    static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(RunClient);
    }
    public static void RunClient(Options opt)
    {
        var client = new TcpClient();
        var currentState = ProtocolState.Start;
        
        client.Connect(opt.Host, opt.Port);
        var stream = client.GetStream();
        stream.ReadTimeout = 2000;
        
        using var reader = new StreamReader(stream, Encoding.UTF8);
        
        while (true)
        {
            // Get user input
            var line = Console.ReadLine();
            
            // Send user Command
            if (line.StartsWith("/"))
            {
                var parts = line.Split(" ");
                var command = parts[0].Substring(1, parts[0].Length - 1);
                var messageType = Enum.Parse(typeof(MessageType), command);
            }
            // Send user message
            else
            {
                var message = line;
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                stream.Write(bytes);
            }
            
            // -------------------------------------------- //
            
            // Receive 
            var receiveBuffer = new byte[2048];
            var length = stream.Read(receiveBuffer, 0, receiveBuffer.Length);
            var responseData = Encoding.UTF8.GetString(receiveBuffer, 0, length);
            
            Console.WriteLine($"[RECEIVED] {responseData}");
        }

    }
} 


