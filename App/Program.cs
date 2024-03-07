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
    static async Task Main(string[] args)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);
        socket.ReceiveTimeout = 1000;
        
        var ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4567);
        
        while (true)
        {
            var message = Encoding.UTF8.GetBytes("Hello, World!");
            await socket.SendToAsync(message, SocketFlags.None, ep);
            Console.WriteLine("Sent message");
            
            Console.WriteLine("Receiving message");
            var buffer = new byte[1024];
            var s = SocketFlags.None;
            var received = socket.ReceiveMessageFrom(buffer, ref s, ref ep);
            var receivedMessage = Encoding.UTF8.GetString(buffer, 0, received.ReceivedBytes);
            
            Console.WriteLine("Received a message", receivedMessage);
        }
        //
        // new Parser(with => with.CaseInsensitiveEnumValues = true)
        //     .ParseArguments<Options>(args)
        //     .WithParsed(o => RunClient(o).Wait());
    }
    
    public static async Task RunClient(Options opt)
    {
        var source = new CancellationTokenSource();
        
        ITransport transport;
        if (opt.Protocol == TransportProtocol.Udp)
        {
            transport = new UdpTransport(opt.Host, opt.Port, source.Token);
        }
        else
        {
           transport = new TcpTransport(opt.Host, opt.Port, source.Token);
        }
        
        var client = new ChatClient(transport, source);
        await client.Start();
    }
} 