using App.Enums;
using App.Models;
using App.Transport;

namespace App;

public class Ipk24ChatProtocol
{
    private readonly ITransport _transport;
    
    private ProtocolState _protocolState;
    private string _displayName;

    public event EventHandler<IBaseModel> OnMessage;
    public event EventHandler<Exception> OnError;
    
    public Ipk24ChatProtocol(ITransport transport)
    {
        _transport = transport;
        
        // Event subscription
        _transport.OnMessage += OnMessageReceived;
    }
    
    public async Task Start(ProtocolState protocolState)
    {
        _protocolState = protocolState;
        await _transport.Start(_protocolState);
    }
    
    public async Task Auth(string username, string password, string displayName)
    {
        await _transport.Auth(new AuthModel() { Username = username, Secret = password, DisplayName = displayName });
    }
    
    public async Task Join(string displayName, string channel)
    {
        await _transport.Join(new JoinModel() { DisplayName = displayName, ChannelId = channel });
    }
    
    public async Task Message(string displayName, string message)
    {
        await _transport.Message(new MessageModel() { DisplayName = displayName, Content= message });
    }
    
    public async Task Disconnect()
    {
        await _transport.Disconnect();
    }
    
    public async void OnMessageReceived(object? sender, IBaseModel model)
    {
        switch (model)
        {
            case JoinModel _:
                if (_protocolState is not ProtocolState.Open)
                {
                    throw new Exception("Invalid state");
                }

                await _transport.Error(new MessageModel() { Content = "Invalid JOIN message type", DisplayName = _displayName});
                _protocolState = ProtocolState.Error;
                break;
            case AuthModel _:
                if (_protocolState is not ProtocolState.Open)
                {
                    throw new Exception("Invalid state");
                }
                
                await _transport.Error(new MessageModel() { Content = "Invalid AUTH message type", DisplayName = _displayName});
                _protocolState = ProtocolState.Error;
                break;
            case MessageModel messageModel:
                if (_protocolState is not ProtocolState.Open)
                {
                    throw new Exception("Invalid state");
                }
                
                // Console.WriteLine($"[RECEIVED] {messageModel.DisplayName}: {messageModel.Content}");
                OnMessage?.Invoke(this, messageModel);
                break;
            case ErrorModel errorModel:
                if (_protocolState is not (ProtocolState.Open or ProtocolState.Auth))
                {
                    throw new Exception("Invalid state");
                }
                
                // Console.WriteLine($"Error from {errorModel.DisplayName}: {string.Join(" ", errorModel.Content)}");
                _protocolState = ProtocolState.End;
                OnMessage?.Invoke(this, errorModel);
                break;
            case ReplyModel replyModel:
                if (_protocolState is not (ProtocolState.Auth or ProtocolState.Open))
                {
                    throw new Exception("Invalid state");
                }
                
                if (replyModel.Status)
                {
                    
                    // Console.WriteLine($"Success: {replyModel.Content}, unlocking the queue, semaphore: {_messageQueue._semaphore}");
                    OnMessage?.Invoke(this, replyModel);
                    _protocolState = ProtocolState.Open;
                    // _messageQueue.Unlock();
                    // await _messageQueue.DequeueMessageAsync(); 
                }
                else
                {
                    OnMessage?.Invoke(this, replyModel);
                    if (_protocolState is ProtocolState.Auth)
                    {
                        throw new Exception("Authentication error");
                    }
                    
                    throw new Exception("Join error");
                }
                break;
            case ByeModel _:
                if (_protocolState is ProtocolState.Open)
                {
                    _protocolState = ProtocolState.End;
                    break;
                }
                
                throw new Exception("Invalid state");
            default:
                throw new Exception("Unknown message type received.");
        }
        
        if (_protocolState == ProtocolState.End)
        {
            await _transport.Disconnect();
            // await _cancellationTokenSource.CancelAsync();
        }
    }
}