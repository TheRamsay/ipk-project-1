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
    private readonly HashSet<short> _processedMessages = new();

    private PendingMessage? _pendingMessage;
    private short _messageIdSequence;
    private ProtocolStateBox _protocolState;

    public event EventHandler<IBaseModel>? OnMessageReceived;
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

    public async Task Connect()
    {
        _client.Connect(_options.Host, _options.Port);
        Console.WriteLine("Connected to server");
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
        await Send(new UdpByeModel() { Id = _messageIdSequence++ });
    }

    public async Task Start(ProtocolStateBox protocolState)
    {
        _protocolState = protocolState;
        var ipv4 = (await Dns.GetHostAddressesAsync(_options.Host)).FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        if (ipv4 == null)
        {
            throw new ServerUnreachableException("Invalid server address");
        }
        _client.Client.Bind(new IPEndPoint(ipv4, 0));
        // _client.Connect(new IPEndPoint(ipv4, 0));
        
        // Console.WriteLine(_client.Client.LocalEndPoint);
        // await Connect();

        while (!_cancellationToken.IsCancellationRequested)
        {
            IPEndPoint receiveFrom = new IPEndPoint(IPAddress.Any, 0);
            Console.WriteLine("Waiting for message...");
            var response = await _client.ReceiveAsync(_cancellationToken);
            // var response = _client.ReceiveAsync(ref receiveFrom);
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
            
            Console.WriteLine("Received message:" + parsedData.ToString());
            // var parsedData = ParseMessage(response);

            if (parsedData is UdpConfirmModel confirmModel)
            {
                OnMessageConfirmed?.Invoke(this, confirmModel);
            }

            if (parsedData is IModelWithId modelWithId)
            {
                // Received message is already processed, we just confirm it and continue
                if (_processedMessages.Contains(modelWithId.Id))
                {
                    await Send(new UdpConfirmModel { RefMessageId = modelWithId.Id });
                    continue;
                }
                
                await Send(new UdpConfirmModel { RefMessageId = modelWithId.Id });

                _processedMessages.Add(modelWithId.Id);
                switch (parsedData)
                {
                    case UdpAuthModel data:
                        OnMessageReceived?.Invoke(this,
                            new AuthModel()
                                { DisplayName = data.DisplayName, Secret = data.Secret, Username = data.Username });
                        break;
                    case UdpJoinModel data:
                        OnMessageReceived?.Invoke(this,
                            new JoinModel() { ChannelId = data.ChannelId, DisplayName = data.DisplayName });
                        break;
                    case UdpMessageModel data:
                        OnMessageReceived?.Invoke(this,
                            new MessageModel() { Content = data.Content, DisplayName = data.DisplayName });
                        break;
                    case UdpErrorModel data:
                        OnMessageReceived?.Invoke(this,
                            new MessageModel() { Content = data.Content, DisplayName = data.DisplayName });
                        break;
                    case UdpReplyModel data:
                        if (_protocolState.State == ProtocolState.Auth)
                        {
                            // _client.Connect(_options.Host, from.Port);
                            _options.Port = (ushort)from.Port;
                            Console.WriteLine($"Reconnecting to different port for authenticated communication (PORT: ${from.Port})");
                            // _client.Client.Bind(new IPEndPoint(IPAddress.Any, 4568));
                            // _client.Connect(_options.Host, from.Port);
                            // _client.Close();
                            // _client = new UdpClient();
                            // _client.Connect(_options.Host, from.Port);
                        }

                        OnMessageReceived?.Invoke(this, new ReplyModel() { Content = data.Content, Status = data.Status });
                        break;
                    case UdpByeModel _:
                        OnMessageReceived?.Invoke(this, new ByeModel());
                        break;
                    default:
                        throw new InvalidMessageReceivedException("Unknown UDP message type");
                }
            }
        }
    }

    private async Task Send(IBaseUdpModel data)
    {
        var buffer = IBaseUdpModel.Serialize(data);
        
       var ipv4 = Dns.GetHostAddresses(_options.Host).First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        Console.WriteLine("Sending message to ip: " + ipv4 + " port: " + _options.Port);
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
                await Send((IBaseUdpModel)data);
            }
            else
            {
                throw new ServerUnreachableException("Max retries reached, message not delivered");
            }

        }
    }
}