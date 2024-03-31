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

    public event EventHandler<IBaseModel>? OnMessageReceived;
    public event EventHandler? OnMessageDelivered;
    public event EventHandler? OnConnected;

    public TcpTransport(Options options, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _options = options;
    }

    public async Task Start(ProtocolStateBox protocolState)
    {
        await _client.ConnectAsync(_options.Host, _options.Port, _cancellationToken);
        _stream = _client.GetStream();
        OnConnected?.Invoke(this, EventArgs.Empty);
        ClientLogger.LogDebug("Connected to server");

        // TODO: uh oh 20_000
        _stream.ReadTimeout = 20000;

        while (true)
        {
            var receivedData = await ReadUntilCrlf();

            // This means the server has closed the connection
            if (receivedData is null)
            {
                ClientLogger.LogDebug("Server has closed the connection");
                throw new ServerUnreachableException("Server has closed the connection");
            }

            var model = ParseMessage(receivedData);

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
    {

        var parts = message.Split(" ");
        var partsUpper = message.ToUpper().Split(" ");
        
        
       switch (partsUpper)
        {
            case ["JOIN", _, "AS", _]:
                return new JoinModel { ChannelId = parts[1], DisplayName = parts[3]};
            case ["AUTH", _, "AS", _, "USING", _]:
                return new AuthModel { Username = parts[1], Secret = parts[3], DisplayName = parts[5] };
            case ["MSG", "FROM", _, "IS", ..]:
                return new MessageModel { DisplayName = parts[2], Content = string.Join(" ", parts.Skip(4)) };
            case ["ERR", "FROM", _, "IS", ..]:
                return new ErrorModel { DisplayName = parts[2], Content = string.Join(" ", parts.Skip(4)) };
            case ["REPLY", _, "IS", ..]:
                return new ReplyModel { Status = parts[1] == "OK", Content = string.Join(" ", parts.Skip(3)) };
            case ["BYE"]:
                return new ByeModel();
            default:
                throw new InvalidMessageReceivedException($"Unknown message type: {message}");
        };
    }

    private async Task<string?> ReadUntilCrlf()
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
}