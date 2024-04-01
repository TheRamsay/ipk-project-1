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
    static async Task Main(string[] args)
    {
        new Parser(with => with.CaseInsensitiveEnumValues = true)
            .ParseArguments<Options>(args)
            .WithParsed(o => RunClient(o).Wait());
    }

    static async Task<string?> ReadUntilCrlf(Stream _stream)
    {
        if (_stream is null)
        {
            ClientLogger.LogDebug("AAAAAA");
            return null;
        }

        var buffer = new byte[1];
        var prevChar = -1;
        var sb = new StringBuilder();

        while (await _stream.ReadAsync(buffer.AsMemory(0, 1)) != 0)
        {
            var currChar = (int)buffer[0];
            if (prevChar == '\r' && currChar == '\n')
            {
                return sb.ToString().TrimEnd('\r', '\n');
            }
            sb.Append((char)currChar);
            prevChar = currChar;
        }

        return null;
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

        await client.Start();
    }
}