using System.Net;
using System.Net.Sockets;
using System.Text;
using App.Enums;
using App.Input;
using App.Models;
using App.Models.udp;
using App.Transport;
using CommandLine;
using Serilog;

namespace App;
static class Program
{
    static async Task Main(string[] args)
    {
        
        new Parser(with => with.CaseInsensitiveEnumValues = true)
            .ParseArguments<Options>(args)
            .WithParsed(o => RunClient(o).Wait());
    }
    
    public static async Task RunClient(Options opt)
    {
        var source = new CancellationTokenSource();
        
        ITransport transport;
        if (opt.Protocol == TransportProtocol.Udp)
        {
            transport = new UdpTransport(opt, source.Token);
        }
        else
        {
           transport = new TcpTransport(opt, source.Token);
        }
        
        var protocol = new Ipk24ChatProtocol(transport);

        var client = new ChatClient(protocol, new StandardInputReader(), source);
        
        Console.CancelKeyPress += async (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Console.WriteLine("Exiting...");
            if (!client.Finished.Value)
            {
                client.Finished.Value = true;
                await client.Stop();
                await source.CancelAsync();
            }
        };
        
        await client.Start();
    }
} 