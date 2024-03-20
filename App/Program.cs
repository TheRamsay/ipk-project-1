using System.Net;
using System.Net.Sockets;
using System.Text;
using App.Enums;
using App.input;
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
        var source = new CancellationTokenSource();
        
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            source.Cancel();
        };
        
        new Parser(with => with.CaseInsensitiveEnumValues = true)
            .ParseArguments<Options>(args)
            .WithParsed(o => RunClient(o, source).Wait());
    }
    
    public static async Task RunClient(Options opt, CancellationTokenSource source)
    {
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
        await client.Start();
    }
} 