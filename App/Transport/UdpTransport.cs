using System.Net;
using System.Net.Sockets;
using System.Text;
using App.Models;
using App.Models.udp;

namespace App.Transport;

public class UdpTransport : ITransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly CancellationToken _cancellationToken;
    private readonly UdpClient _client = new();
    private readonly ICollection<IModelWithId> _pendingMessages = new List<IModelWithId>();

    private int messageIdSequence = 0;
    
    public event EventHandler<IBaseModel>? OnMessage;
    private event EventHandler<UdpConfirmModel>? OnMessageConfirmed;

    public UdpTransport(string host, int port, CancellationToken cancellationToken)
    {
        _host = host;
        _port = port;
        _cancellationToken = cancellationToken;
        OnMessageConfirmed += OnMessageConfirmedHandler;
    }

    public async Task Connect()
    {
        _client.Connect(_host, _port);
        Console.WriteLine("Connected sheeesh 🦞");
    }
    
    public Task Disconnect()
    {
        _client.Close();
        return Task.FromResult(0);
    }

    public async Task Auth(AuthModel data)
    {
        await Send(new UdpAuthModel
        {
            Username = data.Username,
            DisplayName = data.DisplayName,
            Secret = data.Secret
        });
    }

    public async Task Join(JoinModel data)
    {
        await Send(new UdpJoinModel
        {
            DisplayName = data.DisplayName,
            ChannelId = data.ChannelId
        });
    }

    public async Task Message(MessageModel data)
    {
        await Send(new UdpMessageModel
        {
            DisplayName = data.DisplayName,
            Content = data.Content,
        });
    }

    public async Task Error(MessageModel data)
    {
        await Send(new UdpErrorModel
        {
            DisplayName = data.DisplayName,
            Content = data.Content,
        });
    }

    public async Task Reply(ReplyModel data)
    {
        await Send(new UdpReplyModel
        {
            Content = data.Content,
            Status = data.Status,
            RefMessageId = 1
        });
    }

    public async Task Bye()
    {
        await Send(new UdpByeModel());
    }

    public async Task Start()
    {
        await Connect();
        
        while (!_cancellationToken.IsCancellationRequested)
        {
            var response = await _client.ReceiveAsync();
            var from = response.RemoteEndPoint;
            var receiveBuffer = response.Buffer;
            var parsedData = ParseMessage(receiveBuffer);
            
            Console.WriteLine($"[RECEIVED FROM {from}] {((UdpReplyModel)parsedData).Content}");
            if (parsedData == typeof(UdpConfirmModel))
            {
                OnMessageConfirmed?.Invoke(this, (UdpConfirmModel)parsedData);
            }

            if (parsedData == typeof(IModelWithId))
            {
                await Send(new UdpConfirmModel() { RefMessageId = ((IModelWithId)parsedData).Id });
            }
            
            OnMessage?.Invoke(this, parsedData);

            await Task.Delay(100, _cancellationToken);
        }
    }
    
    private async Task Send(IBaseUdpModel data)
    {
        try
        {
            var buffer = IBaseUdpModel.Serialize(data);
            await _client.SendAsync(buffer);
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed to serialize data: " + e.Message);
        }
    }

    private IBaseUdpModel ParseMessage(byte[] data)
    {
        return IBaseUdpModel.Deserialize(data);
    }
    
    private void OnMessageConfirmedHandler(object sender, UdpConfirmModel data)
    {
        var message = _pendingMessages.FirstOrDefault(m => m.Id == data.RefMessageId);
        if (message is not null)
        {
            _pendingMessages.Remove(message);
        }
    }
}