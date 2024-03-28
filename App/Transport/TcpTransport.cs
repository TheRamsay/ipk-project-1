using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using System.Text;
using App.Enums;
using App.Exceptions;
using App.Models;
using App.Transport;

namespace App.Transport;

public class TcpTransport : ITransport
{
    private readonly CancellationToken _cancellationToken;
    private readonly TcpClient _client = new();
    private readonly Options _options;

    private NetworkStream? _stream;
    private ProtocolStateBox _protocolState;

    public event EventHandler<IBaseModel>? OnMessageReceived;
    public event EventHandler? OnMessageDelivered;

    public TcpTransport(Options options, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _options = options;
    }

    public async Task Start(ProtocolStateBox protocolState)
    {
        _protocolState = protocolState;
        await _client.ConnectAsync(_options.Host, _options.Port);
        _stream = _client.GetStream();
        Console.WriteLine("Connected to server");
        
        // TODO: uh oh 20_000
        _stream.ReadTimeout = 20000;

        while (true)
        {
            var receiveBuffer = new byte[2048];
            var receivedBytes = await _stream.ReadAsync(receiveBuffer, _cancellationToken);
            
            // This means the server has closed the connection
            if (receivedBytes == 0)
            {
                Console.WriteLine("Server has closed the connection");
                throw new ServerUnreachableException("Server has closed the connection");
            }
            
            var responseData = new ArraySegment<byte>(receiveBuffer, 0, receivedBytes).ToArray();
            var responseString = Encoding.UTF8.GetString(responseData);
            
            var model = ParseMessage(responseString);

            try
            {
                ModelValidator.Validate(model);
            }
            catch (ValidationException e)
            {
                throw new InvalidMessageReceivedException(e.Message);
            }

            OnMessageReceived?.Invoke(this, model);
        }
    }

    public void Disconnect()
    {
        _client.Close();
    }

    public async Task Auth(AuthModel data)
    {
        await Send($"AUTH {data.Username} AS {data.DisplayName} USING {data.Secret}");
    }
    
    public async Task Join(JoinModel data)
    {
        await Send($"JOIN {data.ChannelId} AS {data.DisplayName}");
    }

    public async Task Message(MessageModel data)
    {
        await Send($"MSG FROM {data.DisplayName} IS {data.Content}");
    }

    public async Task Error(ErrorModel data)
    {
        await Send($"ERR FROM {data.DisplayName} IS {data.Content}");
    }

    public async Task Reply(ReplyModel data)
    {
        var statusStr = data.Status ? "OK" : "NOK";
        await Send($"REPLY {statusStr} IS {data.Content}");
    }

    public async Task Bye()
    {
        await Send("BYE");
    }

    private async Task Send(string message)
    {
        message = $"{message}\r\n";
        var bytes = Encoding.UTF8.GetBytes(message);
        if (_stream != null)
        {
            await _stream.WriteAsync(bytes);
            OnMessageDelivered?.Invoke(this, EventArgs.Empty);
        }
    }

    private IBaseModel ParseMessage(string message)
        => message.ToUpper().Split(" ") switch
        {
            ["JOIN", var channelId, "AS", var dName] => new JoinModel() { ChannelId = channelId, DisplayName = dName },
            ["AUTH", var userId, "AS", var dName, "USING", var secret] => new AuthModel()
                { Username = userId, Secret = secret, DisplayName = dName },
            ["MSG", "FROM", var dName, "IS", .. var content] => new MessageModel()
                { DisplayName = dName, Content = string.Join(" ", content) },
            ["ERR", "FROM", var dName, "IS", .. var content] => new ErrorModel()
                { DisplayName = dName, Content = string.Join(" ", content) },
            ["REPLY", var status, "IS", .. var content] => new ReplyModel()
                { Status = status == "OK", Content = string.Join(" ", content) },
            ["BYE"] => new ByeModel(),
            _ => throw new InvalidMessageReceivedException($"Unknown message type: {message}")
        };
    
    static string ReadUntilCrlf(StreamReader reader)
    {
        var sb = new StringBuilder();
        
        var prevChar = -1;
        int currChar;
        
        while ((currChar = reader.Read()) != -1)
        {
            if (prevChar == '\r' && currChar == '\n')
            {
                return sb.ToString();
            }
            sb.Append((char)currChar);
            prevChar = currChar;
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }
}