using System.Net;
using System.Net.Sockets;
using System.Text;
using App.Models;
using App.Models.udp;

namespace App.Transport;

public class UdpTransport : ITransport
{
    private readonly CancellationToken _cancellationToken;
    private readonly UdpClient _client = new();
    private readonly Options _options;
    
    private readonly Dictionary<short, int> _pendingMessages = new();
    private readonly HashSet<short> _processedMessages = new();

    private short _messageIdSequence = 0;

    public event EventHandler<IBaseModel>? OnMessage;
    private event EventHandler<UdpConfirmModel>? OnMessageConfirmed;
    public event EventHandler<IModelWithId> OnTimeoutExpired;

    public UdpTransport(Options options, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _options = options;
        OnMessageConfirmed += OnMessageConfirmedHandler;
        OnTimeoutExpired += OnTimeoutExpiredHandler;
    }

    public async Task Connect()
    {
        _client.Connect(_options.Host, _options.Port);
        Console.WriteLine("Connected sheeesh 🦞");
    }

    public async Task Disconnect()
    {
        await Bye(); 
        _client.Close();
    }

    public async Task Auth(AuthModel data)
    {
        await Send(new UdpAuthModel
        {
            Id = _messageIdSequence++,
            Username = data.Username,
            DisplayName = data.DisplayName,
            Secret = data.Secret
        });
    }

    public async Task Join(JoinModel data)
    {
        await Send(new UdpJoinModel
        {
            Id = _messageIdSequence++,
            DisplayName = data.DisplayName,
            ChannelId = data.ChannelId
        });
    }

    public async Task Message(MessageModel data)
    {
        await Send(new UdpMessageModel
        {
            Id = _messageIdSequence++,
            DisplayName = data.DisplayName,
            Content = data.Content,
        });
    }

    public async Task Error(MessageModel data)
    {
        await Send(new UdpErrorModel
        {
            Id = _messageIdSequence++,
            DisplayName = data.DisplayName,
            Content = data.Content,
        });
    }

    public async Task Reply(ReplyModel data)
    {
        await Send(new UdpReplyModel
        {
            Id = _messageIdSequence++,
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
                if (_processedMessages.Contains(modelWithId.Id))
                {
                    await Send(new UdpConfirmModel { RefMessageId = modelWithId.Id });
                    Console.WriteLine($"Skipping message {modelWithId.Id}, duplicate");
                    return;
                }
                
                await Send(new UdpConfirmModel { RefMessageId = modelWithId.Id });

                _processedMessages.Add(modelWithId.Id);
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
        Console.WriteLine($"Sent message {data}");

        if (data is IModelWithId modelWithId)
        {
            if (!_pendingMessages.TryGetValue(modelWithId.Id, out _))
            {
                Console.WriteLine($"Added message {modelWithId.Id} to pendings");
                _pendingMessages[modelWithId.Id] = 0;
            }

            Task.Run(async () =>
            {
                Console.WriteLine($"Created timeout for message {modelWithId.Id}");
                await Task.Delay(_options.Timeout);
                Console.WriteLine($"Timeout for message {modelWithId.Id} has expired");
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
        if (_pendingMessages.ContainsKey(data.RefMessageId))
        {
            Console.WriteLine($"Message {data.RefMessageId} has been confirmed");
            _pendingMessages.Remove(data.RefMessageId);
        }
    }

    private async void OnTimeoutExpiredHandler(object sender, IModelWithId data)
    {
        if (_pendingMessages.TryGetValue(data.Id, out var retries))
        {
            if (retries < _options.Rentransmissions)
            {
                _pendingMessages[data.Id] = retries + 1;
                Console.WriteLine($"Resending message {data.Id}");
                await Send((IBaseUdpModel)data);
            }
            else
            {
                throw new Exception("Max retries reached, message not delivered");
            }
        }
    }
}