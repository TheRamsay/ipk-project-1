using System.Net;
using System.Net.Sockets;
using System.Text;
using App.Enums;
using App.Models;
using App.Models.udp;
using App.Transport;
using CommandLine;
using Serilog;

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
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.Seq("http://localhost:5341")
            .Enrich.WithProperty("Project", "IPK.Project1")
            .CreateLogger();
        
        Log.Information("Starting the client with options: {@Options}", opt);
        await Log.CloseAndFlushAsync();
        
        var source = new CancellationTokenSource();
        
        ITransport transport;
        if (opt.Protocol == TransportProtocol.Udp)
        {
            transport = new UdpTransport(opt, source.Token);
        }
        else
        {
           transport = new TcpTransport(opt, source.Token, Log.Logger);
        }
        
        var client = new ChatClient(transport, source, Log.Logger);
        await client.Start();
    }
} 