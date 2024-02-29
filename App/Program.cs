using System.Net.Sockets;
using CommandLine;

namespace  IPK.Project1.App;

static class Program
{
    static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(Run);
    }
    public static void Run(Options opt)
    {
        var client = new UdpClient();
        client.Connect(opt.Host, opt.Port);

        while (true)
        {
            
        }

    }
} 


