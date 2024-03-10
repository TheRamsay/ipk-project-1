using System.Net;
using System.Net.Sockets;
using System.Text;
using App.Enums;
using App.Models;
using App.Models.udp;
using App.Transport;
using CommandLine;

namespace App;

static class Program
{
    static void Main(string[] args)
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
        
        var client = new ChatClient(transport, source);
        await client.Start();
    }
} 