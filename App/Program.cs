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
        var transport = new UdpTransport(opt.Host, opt.Port, source.Token);
        await transport.Connect();
        
        await transport.Auth(new AuthModel()
        {
            Username = "A",
            DisplayName = "B",
            Secret = "C"
        });
        // var transport = new TcpTransport(opt.Host, opt.Port, source.Token);

        // var client = new ChatClient(transport, source);
        // await client.Start();
    }
} 