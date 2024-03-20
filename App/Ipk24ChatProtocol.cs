using App.Enums;
using App.Exceptions;
using App.Models;
using App.Transport;

namespace App;

public class Ipk24ChatProtocol
{
    private readonly ITransport _transport;
    private readonly SemaphoreSlim _messageDeliveredSignal = new (0, 1);
    private readonly SemaphoreSlim _messageProcessedSignal = new (0, 1);
    
    private ProtocolState _protocolState;
    private string _displayName = "Pepa";
    
    public event EventHandler<IBaseModel>? OnMessage;
    public event EventHandler<Exception>? OnError;
    
    public Ipk24ChatProtocol(ITransport transport)
    {
        _transport = transport;
        
        // Event subscription
        _transport.OnMessageReceived += OnMessageReceived;
        _transport.OnMessageDelivered += OnMessageDelivered;
    }
    
    public async Task Start()
    {
        _protocolState = ProtocolState.Start;
        await _transport.Start(_protocolState);
    }
    
    private async Task Auth(AuthModel data)
    {
        _displayName = data.DisplayName;
        await WaitForDeliveredAndProcessed(_transport.Auth(data));
    }
    
    private async Task Join(JoinModel data)
    {
        data.DisplayName = _displayName;
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
        switch (_protocolState, model)
        {
            case (ProtocolState.Start, AuthModel data):
                _protocolState = ProtocolState.Auth;
                await Auth(data);
                break;
            case (ProtocolState.WaitForAuth, AuthModel data):
                _protocolState = ProtocolState.Auth;
                await Auth(data);
                break;
            case (ProtocolState.Open, MessageModel data):
                await Message(data);
                break;
            case (ProtocolState.Open, JoinModel data):
                await Join(data);
                break;
            case (ProtocolState.Auth or ProtocolState.Open or ProtocolState.Error or ProtocolState.WaitForAuth, ByeModel):
                _protocolState = ProtocolState.End;
                break;
            default:
                await _transport.Error(new ErrorModel() { Content = "Invalid state transition", DisplayName = _displayName});
                throw new InvalidInputException(_protocolState);
        }
    }
    
    private async Task Receive(IBaseModel model)
    {
        switch (_protocolState, model)
        {
            case (ProtocolState.Auth, ReplyModel { Status: true } data):
                _protocolState = ProtocolState.Open;
                _messageProcessedSignal.Release();
                OnMessage?.Invoke(this, data);
                break;
            case (ProtocolState.Auth, ReplyModel { Status: false } data):
                _protocolState = ProtocolState.WaitForAuth;
                _messageProcessedSignal.Release();
                OnMessage?.Invoke(this, data);
                OnError?.Invoke(this, new Exception("Authentication failed"));
                break;
            case (ProtocolState.Open or ProtocolState.Auth, ErrorModel data):
                _protocolState = ProtocolState.End;
                OnError?.Invoke(this, new Exception($"Error from {data.DisplayName}: {string.Join(" ", data.Content)}"));
                break;
            case (ProtocolState.Open, MessageModel data):
                OnMessage?.Invoke(this, data);
                break;
            case (ProtocolState.Open, ReplyModel data):
                _messageProcessedSignal.Release();
                OnMessage?.Invoke(this, data);
                // TODO: Handle reply
                break;
            case (ProtocolState.Open, ByeModel data):
                _protocolState = ProtocolState.End;
                break;
            default:
                // TODO: prasiacke reseni
                await _transport.Error(new ErrorModel() { Content = "Invalid state transition", DisplayName = _displayName});
                OnError?.Invoke(this, new Exception("Invalid state transition"));
                _protocolState = ProtocolState.End;
                break;
                // throw new Exception("Invalid state transition");
        }
        
        if (_protocolState == ProtocolState.End)
        {
            await Disconnect();
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