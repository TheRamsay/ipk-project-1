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

    private readonly ICollection<(IModelWithId message, int retries)>
        _pendingMessages = new List<(IModelWithId, int)>();

    private short messageIdSequence = 0;

    public event EventHandler<IBaseModel>? OnMessage;
    private event EventHandler<UdpConfirmModel>? OnMessageConfirmed;
    public event EventHandler<IModelWithId> OnTimeoutExpired;

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
            Id = messageIdSequence++,
            Username = data.Username,
            DisplayName = data.DisplayName,
            Secret = data.Secret
        });
    }

    public async Task Join(JoinModel data)
    {
        await Send(new UdpJoinModel
        {
            Id = messageIdSequence++,
            DisplayName = data.DisplayName,
            ChannelId = data.ChannelId
        });
    }

    public async Task Message(MessageModel data)
    {
        await Send(new UdpMessageModel
        {
            Id = messageIdSequence++,
            DisplayName = data.DisplayName,
            Content = data.Content,
        });
    }

    public async Task Error(MessageModel data)
    {
        await Send(new UdpErrorModel
        {
            Id = messageIdSequence++,
            DisplayName = data.DisplayName,
            Content = data.Content,
        });
    }

    public async Task Reply(ReplyModel data)
    {
        await Send(new UdpReplyModel
        {
            Id = messageIdSequence++,
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

            if (parsedData is UdpConfirmModel confirmModel)
            {
                OnMessageConfirmed?.Invoke(this, confirmModel);
            }

            if (parsedData is IModelWithId modelWithId)
            {
                await Send(new UdpConfirmModel() { RefMessageId = modelWithId.Id });

                switch (parsedData)
                {
                    case UdpAuthModel data:
                        OnMessage?.Invoke(this,
                            new AuthModel()
                                { DisplayName = data.DisplayName, Secret = data.Secret, Username = data.Username });
                        break;
                    case UdpJoinModel data:
                        OnMessage?.Invoke(this,
                            new JoinModel() { ChannelId = data.ChannelId, DisplayName = data.DisplayName });
                        break;
                    case UdpMessageModel data:
                        OnMessage?.Invoke(this,
                            new MessageModel() { Content = data.Content, DisplayName = data.DisplayName });
                        break;
                    case UdpErrorModel data:
                        OnMessage?.Invoke(this,
                            new MessageModel() { Content = data.Content, DisplayName = data.DisplayName });
                        break;
                    case UdpReplyModel data:
                        OnMessage?.Invoke(this, new ReplyModel() { Content = data.Content, Status = data.Status });
                        break;
                    case UdpByeModel _:
                        OnMessage?.Invoke(this, new ByeModel());
                        break;
                    default:
                        throw new Exception("Invalid state");
                }
            }
        }
    }

    private async Task Send(IBaseUdpModel data)
    {
        var buffer = IBaseUdpModel.Serialize(data);
        await _client.SendAsync(buffer);

        if (data is IModelWithId modelWithId)
        {
            _pendingMessages.Add((modelWithId, 0));

            Task.Run(async () =>
            {
                await Task.Delay(250);
                OnTimeoutExpired.Invoke(this, modelWithId);
            });
        }
    }

    private IBaseUdpModel ParseMessage(byte[] data)
    {
        return IBaseUdpModel.Deserialize(data);
    }

    private void OnMessageConfirmedHandler(object sender, UdpConfirmModel data)
    {
        var (message, retries) = _pendingMessages.FirstOrDefault(i => i.message.Id == data.RefMessageId);
        if (message is not null)
        {
            _pendingMessages.Remove((message, retries));
        }
    }

    private async void OnTimeoutExpiredHandler(object sender, IModelWithId data)
    {
        var (message, retries) = _pendingMessages.FirstOrDefault((i) => i.message.Id == data.Id);
        if (message is not null)
        {
            if (retries < 3)
            {
                _pendingMessages.Remove((message, retries));
                _pendingMessages.Add((message, retries + 1));
                await Send((IBaseUdpModel)message);
            }
            else
            {
                throw new Exception("Max retries reached, message not delivered");
            }
        }
    }
}