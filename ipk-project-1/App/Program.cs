using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Text;
using App.Enums;
using App.Exceptions;
using App.Input;
using App.Models;
using App.Models.udp;
using App.Transport;
using CommandLine;
using Serilog;

namespace App;
static class Program
{
    static async Task<int> Main(string[] args)
    {
        var statusCode = 0;
        
       if (args.Any(arg => arg is "-h" or "--help"))
       {
           PrintHelp();
           return statusCode;
       }

       new Parser(with => with.CaseInsensitiveEnumValues = true)
            .ParseArguments<Options>(args)
            .WithParsed(o => RunClient(o).Wait())
           .WithNotParsed(errors =>
           {
               foreach (var error in errors)
               {
                   if (error is HelpRequestedError or VersionRequestedError)
                   {
                       PrintHelp();
                   }
                   else
                   {
                       ClientLogger.LogInternalError(error.ToString() ?? string.Empty);
                   }
               }

               statusCode = 1;
           });

       return statusCode;
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

        var protocol = new Ipk24ChatProtocol(transport, source);

        var client = new ChatClient(protocol, new StandardInputReader(), source);

        Console.CancelKeyPress += async (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            if (!client.Finished.Value)
            {
                client.Finished.Value = true;
                await client.Stop();
            }
        };

        var statusCode = await client.Start();
        Environment.Exit(statusCode);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: program_name [options]\n");
        Console.WriteLine("Options:");
        Console.WriteLine("  -t <tcp|udp>\tTransport protocol used for connection");
        Console.WriteLine("  -s <IP_address|hostname>\tServer IP or hostname");
        Console.WriteLine("  -p <port_number>\tServer port (default: 4567)");
        Console.WriteLine("  -d <timeout_value>\tUDP confirmation timeout (default: 250)");
        Console.WriteLine("  -r <num_retransmissions>\tMaximum number of UDP retransmissions (default: 3)");
        Console.WriteLine("  -h, --help\tPrints program help output and exits");    
    }
}