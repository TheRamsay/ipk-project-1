﻿using System.Net.Sockets;
using System.Text;
using App.Enums;
using App.Models;
using App.Transport;

namespace App.Transport;

public class TcpTransport : ITransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly CancellationToken _cancellationToken;
    private readonly TcpClient _client = new();

    private NetworkStream? _stream;

    public event EventHandler<IBaseModel> OnMessage;

    public TcpTransport(string host, int port, CancellationToken cancellationToken)
    {
        _host = host;
        _port = port;
        _cancellationToken = cancellationToken;
    }

    public async Task Start()
    {
        await _client.ConnectAsync(_host, _port, _cancellationToken);
        _stream = _client.GetStream();
        
        _stream.ReadTimeout = 20000;
        Console.WriteLine("Connected sheeesh 🦞");

        while (!_cancellationToken.IsCancellationRequested)
        {
            var receiveBuffer = new byte[2048];
            var length = await _stream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);
            var responseData = Encoding.UTF8.GetString(receiveBuffer, 0, length);
            OnMessage.Invoke(this, ParseMessage(responseData));

            await Task.Delay(100, _cancellationToken);
        }
    }

    public async Task Disconnect()
    {
        await Bye();
        if (_stream is not null)
        {
            await _stream.DisposeAsync();
            _client.Dispose();
        }
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

    public async Task Error(MessageModel data)
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
            await _stream.WriteAsync(bytes, _cancellationToken);
        }
    }

    private IBaseModel ParseMessage(string message)
        => message.Split(" ") switch
        {
            ["JOIN", var channelId, "AS", var dName] => new JoinModel() { ChannelId = channelId, DisplayName = dName },
            ["AUTH", var userId, "AS", var dName, "USING", var secret] => new AuthModel()
                { Username = userId, Secret = secret, DisplayName = dName },
            ["MSG", "FROM", var dName, "IS", .. var content] => new MessageModel()
                { DisplayName = dName, Content = string.Join(" ", content) },
            ["ERR", "FROM", var dName, "IS", .. var content] => new MessageModel()
                { DisplayName = dName, Content = string.Join(" ", content) },
            ["REPLY", var status, "IS", .. var content] => new ReplyModel()
                { Status = status == "OK", Content = string.Join(" ", content) },
            ["BYE"] => new ByeModel(),
            _ => throw new Exception("Unknown message type received.")
        };
}