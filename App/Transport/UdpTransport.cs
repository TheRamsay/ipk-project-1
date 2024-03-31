using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using System.Text;
using App.Enums;
using App.Exceptions;
using App.Models;
using App.Models.udp;

namespace App.Transport;

public class UdpTransport : ITransport
{
    private readonly CancellationToken _cancellationToken;
    private readonly UdpClient _client = new();
    private readonly Options _options;
    private readonly SemaphoreSlim _timeoutExpiredSignal = new(0, 1);
    private readonly HashSet<short> _processedMessages = new();

    private PendingMessage? _pendingMessage;
    private short _messageIdSequence;
    private ProtocolStateBox _protocolState;

    public event EventHandler<IBaseModel>? OnMessageReceived;
    public event EventHandler? OnConnected;
    private event EventHandler<UdpConfirmModel>? OnMessageConfirmed;
    public event EventHandler<IModelWithId> OnTimeoutExpired;
    public event EventHandler? OnMessageDelivered;

    public UdpTransport(Options options, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _options = options;
        OnMessageConfirmed += OnMessageConfirmedHandler;
        OnTimeoutExpired += OnTimeoutExpiredHandler;
    }

    public void Disconnect()
    {
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

    public async Task Error(ErrorModel data)
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
        await Send(new UdpByeModel { Id = _messageIdSequence++ });
    }

    private async Task MonitorTimeout()
    {
        await _timeoutExpiredSignal.WaitAsync(_cancellationToken);
        throw new ServerUnreachableException("Max retries reached, message not delivered");
    }

    public async Task Start(ProtocolStateBox protocolState)
    {
        _protocolState = protocolState;
        OnConnected?.Invoke(this, EventArgs.Empty);
        await await Task.WhenAny(Receive(), MonitorTimeout());
    }

    private async Task Receive()
    {
        var ipv4 = (await Dns.GetHostAddressesAsync(_options.Host)).FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        if (ipv4 == null)
        {
            throw new ServerUnreachableException("Invalid server address");
        }
        _client.Client.Bind(new IPEndPoint(ipv4, 0));
        
        while (!_cancellationToken.IsCancellationRequested)
        {
            ClientLogger.LogDebug("Waiting for message...");
            var response = await _client.ReceiveAsync(_cancellationToken);
            var from = response.RemoteEndPoint;
            var receiveBuffer = response.Buffer;
            var parsedData = ParseMessage(receiveBuffer);

            try
            {
                ModelValidator.Validate(parsedData);
            }
            catch (ValidationException e)
            {
                throw new InvalidMessageReceivedException($"Unable to validate received message: {e.Message}");
            }

            ClientLogger.LogDebug("Received message:" + parsedData);

            switch (parsedData)
            {
                case UdpConfirmModel confirmModel:
                    OnMessageConfirmed?.Invoke(this, confirmModel);
                    break;
                case IModelWithId modelWithId when _processedMessages.Contains(modelWithId.Id):
                    await Send(new UdpConfirmModel { RefMessageId = modelWithId.Id });
                    continue;
                case IModelWithId modelWithId:
                {
                    var model = UdpModelMapper(parsedData);
                    if (model is ReplyModel && _protocolState.State is ProtocolState.Auth)
                    {
                        _options.Port = (ushort)from.Port;
                        ClientLogger.LogDebug($"Reconnecting to different port for authenticated communication (PORT: ${from.Port})");
                    }

                    await Send(new UdpConfirmModel { RefMessageId = modelWithId.Id });
                    _processedMessages.Add(modelWithId.Id);
                    OnMessageReceived?.Invoke(this, model);
                    break;
                }
            }
        }
    }

    private async Task Send(IBaseUdpModel data)
    {
        var buffer = IBaseUdpModel.Serialize(data);

        var ipv4 = Dns.GetHostAddresses(_options.Host).First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        ClientLogger.LogDebug("Sending" + data + "to ip: " + ipv4 + " port: " + _options.Port);
        var sendTo = new IPEndPoint(ipv4, _options.Port);
        await _client.SendAsync(buffer, sendTo);

        if (data is IModelWithId modelWithId)
        {
            // If it is first time sending this message, create a new pending message
            _pendingMessage ??= new PendingMessage { Model = modelWithId, Retries = 0 };

            Task.Run(async () =>
            {
                await Task.Delay(_options.Timeout, _cancellationToken);
                OnTimeoutExpired.Invoke(this, modelWithId);
            });
        }
    }

    private IBaseUdpModel ParseMessage(byte[] data)
    {
        return IBaseUdpModel.Deserialize(data);
    }

    private void OnMessageConfirmedHandler(object? sender, UdpConfirmModel data)
    {
        if (_pendingMessage?.Model.Id != data.RefMessageId)
        {
            // DD rekl ze toto se netestuje
            // throw new TransportError("Received confirmation for unknown message");
            return;
        }

        OnMessageDelivered?.Invoke(this, EventArgs.Empty);
        _pendingMessage = null;
    }

    private async void OnTimeoutExpiredHandler(object? sender, IModelWithId data)
    {
        if (_pendingMessage?.Model.Id == data.Id)
        {
            if (_pendingMessage?.Retries < _options.RetryCount)
            {
                _pendingMessage.Retries++;
                ClientLogger.LogDebug("Retransmissioin");
                await Send((IBaseUdpModel)data);
            }
            else
            {
                _timeoutExpiredSignal.Release();
            }
        }
    }

    private IBaseModel UdpModelMapper(IBaseUdpModel udpModel)
    {
        return udpModel switch
        {
            UdpAuthModel data => new AuthModel
            {
                DisplayName = data.DisplayName, Secret = data.Secret, Username = data.Username
            },
            UdpJoinModel data => new JoinModel { ChannelId = data.ChannelId, DisplayName = data.DisplayName },
            UdpMessageModel data => new MessageModel { Content = data.Content, DisplayName = data.DisplayName },
            UdpErrorModel data => new ErrorModel { Content = data.Content, DisplayName = data.DisplayName },
            UdpReplyModel data => new ReplyModel { Content = data.Content, Status = data.Status },
            UdpByeModel _ => new ByeModel(),
            _ => throw new InvalidMessageReceivedException("Unknown UDP message type")
        };
    }
}