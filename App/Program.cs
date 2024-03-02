using System.Net.Sockets;
using System.Text;
using CommandLine;
using IPK.Project1.App.Enums;

namespace IPK.Project1.App;

static class Program
{
    static void Main(string[] args)
    {
        try
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed((o) =>
                {
                    RunClient(o).Wait();
                });
        } catch (Exception e)
        {
           Console.WriteLine($"ERROR: {e}");
           // Console.Error.WriteLine($"ERROR: {e}");
        }
    }
    
    public static async Task RunClient(Options opt)
    {
        var client = new TcpClient();
        await client.ConnectAsync(opt.Host, opt.Port);
        
        Console.WriteLine("CONNECTED SHEESH");

        var transferState = new TransferState()
        {
            DisplayName = "pepik123",
            Client = client,
            CancellationTokenSource = new CancellationTokenSource()
        };
        
        Task stdinRead = ReadInputAsync(transferState);
        Task serverReceive = ReceiveServerAsync(transferState);

        await Task.WhenAll(stdinRead, serverReceive);

        client.Close();
    }

    static async Task ReadInputAsync(TransferState transferState)
    {
        var stream = transferState.Client.GetStream();
        stream.ReadTimeout = 20000;
        
        while (!transferState.CancellationTokenSource.Token.IsCancellationRequested || transferState.State != ProtocolState.End)
        {
            var line = Console.ReadLine();

            if (transferState.State == ProtocolState.End)
            {
                continue;
            }

            if (line is null)
            {
                if (transferState.State is ProtocolState.Auth or ProtocolState.Error)
                {
                    var message = "BYE\r\n";
                    byte[] bytes = Encoding.UTF8.GetBytes(message);
                    await stream.WriteAsync(bytes);
                    return;
                }
                
                throw new Exception("Invalid state");
            }
            
            // Send user Command
            if (line.StartsWith('/'))
            {
                var parts = line.Split(" ");
                var command = parts[0].Substring(1, parts[0].Length - 1);

                switch (command)
                {
                    case "auth":
                        if (transferState.State == ProtocolState.Start)
                        {
                            var message = $"AUTH {parts[1]} AS {parts[3]} USING {parts[2]}\r\n";
                            transferState.DisplayName = parts[3];
                            byte[] bytes = Encoding.UTF8.GetBytes(message);
                            await stream.WriteAsync(bytes);

                            transferState.State = ProtocolState.Auth;
                            break;
                        }
                        
                        throw new Exception("Invalid state");
                    case "join":
                        if (transferState.State == ProtocolState.Open)
                        {
                            var message = $"JOIN {parts[1]} AS {transferState.DisplayName}\r\n";
                            byte[] bytes = Encoding.UTF8.GetBytes(message);
                            await stream.WriteAsync(bytes);
                            break;
                        }
                        
                        throw new Exception("Invalid state");
                    case "rename":
                        transferState.DisplayName = parts[1];
                        break;
                    case "help":
                        Console.WriteLine("😼😼😼😼😼😼");
                        break;
                    case "end":
                        var endMessage = $"BYE\r\n";
                        byte[] endBytes = Encoding.UTF8.GetBytes(endMessage);
                        await stream.WriteAsync(endBytes);
                        await transferState.CancellationTokenSource.CancelAsync();
                        break;
                    default:
                        throw new Exception("Invalid state");
                }
            }
            else
            {
                if (transferState.State == ProtocolState.Open)
                {
                    var message = $"MSG FROM {transferState.DisplayName} IS {line}\r\n";
                    byte[] bytes = Encoding.UTF8.GetBytes(message);
                    await stream.WriteAsync(bytes);
                }
                else
                { 
                    throw new Exception("Invalid state");
                }
            }

            await Task.Delay(100);
        }
    }

    static async Task ReceiveServerAsync(TransferState transferState)
    {
        var stream = transferState.Client.GetStream();
        stream.ReadTimeout = 20000;
        
        while (!transferState.CancellationTokenSource.Token.IsCancellationRequested || transferState.State != ProtocolState.End)
        {
            var receiveBuffer = new byte[2048];
            var length = await stream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);
            var responseData = Encoding.UTF8.GetString(receiveBuffer, 0, length);

            string[] copy;
            var message = String.Empty;
            byte[] bytes;
            var messageParts = responseData.Split(" ");

            switch (messageParts)
            {
                case ["JOIN", var channelId, "AS", var dName]:
                    if (transferState.State != ProtocolState.Open)
                    {
                        throw new Exception("Invalid state");
                    }
                    
                    message = $"ERR FROM {transferState.DisplayName} IS Invalid JOIN message type\r\n";
                    bytes = Encoding.UTF8.GetBytes(message);
                    await stream.WriteAsync(bytes);
                    transferState.State = ProtocolState.Error;
                    break;
                case ["AUTH", var userId, "AS", var dName, "USING", var secret]:
                    if (transferState.State != ProtocolState.Open)
                    {
                        throw new Exception("Invalid state");
                    }
                    
                    message = $"ERR FROM {transferState.DisplayName} IS Invalid AUTH message type\r\n";
                    bytes = Encoding.UTF8.GetBytes(message);
                    await stream.WriteAsync(bytes);
                    transferState.State = ProtocolState.Error;
                    break;
                case ["MSG", "FROM", var dName, "IS", ..]:
                    if (transferState.State != ProtocolState.Open)
                    {
                        throw new Exception("Invalid state");
                    }
                    
                    copy = new string[messageParts.Length - 4];
                    Array.Copy(messageParts, 4, copy, 0, messageParts.Length - 4);
                    Console.WriteLine($"[RECEIVED] {dName}: {string.Join(" ", copy)}");
                    break;
                case ["ERR", "FROM", var dName, "IS", ..]:
                    if (transferState.State is not (ProtocolState.Open or ProtocolState.Auth))
                    {
                        throw new Exception("Invalid state");
                    }
                    
                    copy = new string[messageParts.Length - 4];
                    Array.Copy(messageParts, 4, copy, 0, messageParts.Length - 4);
                    // Console.Error.WriteLine($"Error from {dName}: {string.Join(" ", copy)}");
                    Console.WriteLine($"Error from {dName}: {string.Join(" ", copy)}");

                    message = "BYE\r\n";
                    bytes = Encoding.UTF8.GetBytes(message);
                    await stream.WriteAsync(bytes);
                    transferState.State = ProtocolState.End;
                    throw new Exception("Invalid state");
                case ["REPLY", var status,  "IS", ..]:
                    if (transferState.State is not (ProtocolState.Auth or ProtocolState.Open))
                    {
                        throw new Exception("Invalid state");
                    }
                
                    copy = new string[messageParts.Length - 3];
                    Array.Copy(messageParts, 3, copy, 0, messageParts.Length - 3);
                    
                    if (status == "NOK")
                    {
                        Console.WriteLine($"Failure: {string.Join(" ", copy)}");
                        // Console.Error.WriteLine($"Failure: {string.Join(" ", copy)}");
                        throw new Exception("Authentication error");
                    } 
                    
                    if (status == "OK")
                    {
                        Console.WriteLine($"Success: {string.Join(" ", copy)}");
                        // Console.Error.WriteLine($"Success: {string.Join(" ", copy)}");
                        transferState.State = ProtocolState.Open;
                    }
                    else
                    {
                        throw new Exception("Invalid REPLY status");
                    }

                    break;
                case ["BYE"]:
                    if (transferState.State == ProtocolState.Open)
                    {
                        transferState.State = ProtocolState.End;
                        break;
                    }
                    
                    throw new Exception("Invalid state");
                default:
                    throw new Exception("Unknown message type received.");
            }
            
            await Task.Delay(100);
        }
    }
} 