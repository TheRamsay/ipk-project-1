using App.Enums;
using App.Exceptions;
using App.Models;
using App.Transport;

namespace App;

public class Ipk24ChatProtocol
{
    private readonly ITransport _transport;
    private readonly SemaphoreSlim _messageDeliveredSignal = new(0, 1);
    private readonly SemaphoreSlim _messageProcessedSignal = new(0, 1);
    private readonly SemaphoreSlim _endSignal = new(0, 1);
    private Exception? ExceptionToThrow;

    private ProtocolStateBox _protocolState;
    private readonly string _displayName = "Pepa";

    public event EventHandler<IBaseModel>? OnMessage;
    public event EventHandler OnConnected;
    
    public Ipk24ChatProtocol(ITransport transport)
    {
        _transport = transport;

        // Event subscription
        _transport.OnMessageReceived += OnMessageReceived;
        _transport.OnMessageDelivered += OnMessageDelivered;
        _transport.OnConnected += (sender, args) => OnConnected?.Invoke(sender, args);
    }

    public async Task Start()
    {
        _protocolState = new ProtocolStateBox(ProtocolState.Start);
        try
        {
            await await Task.WhenAny(_transport.Start(_protocolState), CheckErrors());
        }
        catch (InvalidMessageReceivedException e)
        {
            await WaitForDelivered(_transport.Error(new ErrorModel { Content = e.Message, DisplayName = _displayName }));
            throw;
        }
    }

    private async Task Auth(AuthModel data)
    {
        await WaitForDeliveredAndProcessed(_transport.Auth(data));
    }

    private async Task Join(JoinModel data)
    {
        await WaitForDeliveredAndProcessed(_transport.Join(data));
    }

    private async Task Message(MessageModel data)
    {
        await WaitForDelivered(_transport.Message(data));
    }

    public async Task Disconnect()
    {
        // Send bye, wait for delivered and disconnect
        await WaitForDelivered(_transport.Bye());
        _transport.Disconnect();
    }

    private async Task WaitForDelivered(Task task)
    {
        var tasks = new[] { _messageDeliveredSignal.WaitAsync(), task };
        await Task.WhenAll(tasks);
    }

    private async Task WaitForDeliveredAndProcessed(Task task)
    {
        var tasks = new[] { _messageDeliveredSignal.WaitAsync(), _messageProcessedSignal.WaitAsync(), task };
        await Task.WhenAll(tasks);
    }

    public async Task Send(IBaseModel model)
    {
        switch (_protocolState.State, model)
        {
            case (ProtocolState.Start, AuthModel data):
                _protocolState.SetState(ProtocolState.Auth);
                await Auth(data);
                break;
            case (ProtocolState.WaitForAuth, AuthModel data):
                _protocolState.SetState(ProtocolState.Auth);
                await Auth(data);
                break;
            case (ProtocolState.Open, MessageModel data):
                await Message(data);
                break;
            case (ProtocolState.Open, JoinModel data):
                await Join(data);
                break;
            case (ProtocolState.Auth or ProtocolState.Open or ProtocolState.Error or ProtocolState.WaitForAuth, ByeModel):
                _protocolState.SetState(ProtocolState.End);
                break;
            default:
                throw new InvalidInputException(_protocolState.State);
        }
    }

    private async Task CheckErrors()
    {
        ClientLogger.LogDebug("Waiting for end signal");
        await _endSignal.WaitAsync();
        ClientLogger.LogDebug("Received end signal");
        
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }
    }
    
    private async Task Receive(IBaseModel model)
    {
        switch (_protocolState.State, model)
        {
            case (ProtocolState.Auth, ReplyModel { Status: true } data):
                _protocolState.SetState(ProtocolState.Open);
                _messageProcessedSignal.Release();
                OnMessage?.Invoke(this, data);
                break;
            case (ProtocolState.Auth, ReplyModel { Status: false } data):
                _protocolState.SetState(ProtocolState.WaitForAuth);
                _messageProcessedSignal.Release();
                OnMessage?.Invoke(this, data);
                break;
            case (ProtocolState.Open or ProtocolState.Auth, ErrorModel data):
                ExceptionToThrow = new ServerException(data);
                _protocolState.SetState(ProtocolState.End);
                break;
            case (ProtocolState.Open, MessageModel data):
                OnMessage?.Invoke(this, data);
                break;
            case (ProtocolState.Open, ReplyModel data):
                // We are currently not waiting for any reply, so we can ignore it
                if (_messageProcessedSignal.CurrentCount != 0)
                {
                    break;
                }
                _messageProcessedSignal.Release();
                OnMessage?.Invoke(this, data);
                break;
            case (ProtocolState.Open, ByeModel data):
                _protocolState.SetState(ProtocolState.End);
                break;
            default:
                ExceptionToThrow = new InvalidMessageReceivedException($"No action for {model} in state {_protocolState.State}");
                _protocolState.SetState(ProtocolState.End);
                break;
        }

        if (_protocolState.State == ProtocolState.End)
        {
            _endSignal.Release();
        }
    }

    private void OnMessageDelivered(object? sender, EventArgs args)
    {
        _messageDeliveredSignal.Release();
    }

    private async void OnMessageReceived(object? sender, IBaseModel model)
    {
        await Receive(model);
    }
}