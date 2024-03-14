using App.Enums;
using App.input;
using App.Models;
using App.Models.udp;
using App.Transport;

namespace App;

public class ChatClient
{
    private readonly ITransport _transport;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IStandardInputReader _standardInputReader;
    private readonly MessageQueue _messageQueue = new();
    
    private ProtocolState _protocolState;
    private string _displayName = String.Empty;

    public ChatClient(ITransport transport, IStandardInputReader standardInputReader, CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
        _transport = transport;
        _standardInputReader = standardInputReader;
        
        // Event subscription
        _transport.OnMessage += OnMessageReceived;
        _transport.OnSendingReady += OnSendingReady;
        
        // Initial state
        _protocolState = ProtocolState.Start;
    }

    public async Task Start()
    {
        Task transportTask = _transport.Start(_protocolState);
        Task stdinTask = ReadInputAsync();
        
        try
        {
            await await Task.WhenAny(transportTask, stdinTask);
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERROR: {e}");
        }
        finally
        {
            await _transport.Disconnect();
            await _cancellationTokenSource.CancelAsync();
        }
    }
    
    private async Task ReadInputAsync()
    {
        while (_protocolState is not ProtocolState.End || !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            var line = _standardInputReader.ReadLine();

            // Eof reached
            if (line is null)
            {
                await _transport.Disconnect();
                return;
            }

            if (line.Length == 0)
            {
                throw new Exception("Messages can't be empty.");
            }

            var command = UserCommandModel.ParseCommand(line);
            switch (command.Command)
            {
                case UserCommand.Auth:
                    await _messageQueue.EnqueueMessageAsync(Auth(command));
                    break;
                case UserCommand.Join:
                    await _messageQueue.EnqueueMessageAsync(Join(command));
                    break;
                case UserCommand.Rename:
                    await _messageQueue.EnqueueMessageAsync(Rename(command));
                    break;
                case UserCommand.Help:
                    Console.WriteLine("😼😼😼😼😼😼");
                    break;
                case UserCommand.End:
                    await _transport.Bye();
                    await _cancellationTokenSource.CancelAsync();
                    break;
                case UserCommand.Message:
                    await _messageQueue.EnqueueMessageAsync(Message(command));
                    break;
                default:
                    throw new Exception("Invalid state");
            }

            await Task.Delay(100);
        }
    }

    private async void OnMessageReceived(object? sender, IBaseModel args)
    {
        
        switch (args)
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
                
                Console.WriteLine($"[RECEIVED] {messageModel.DisplayName}: {messageModel.Content}");
                break;
            case ErrorModel errorModel:
                if (_protocolState is not (ProtocolState.Open or ProtocolState.Auth))
                {
                    throw new Exception("Invalid state");
                }
                
                Console.WriteLine($"Error from {errorModel.DisplayName}: {string.Join(" ", errorModel.Content)}");
                
                _protocolState = ProtocolState.End;
                break;
            case ReplyModel replyModel:
                if (_protocolState is not (ProtocolState.Auth or ProtocolState.Open))
                {
                    throw new Exception("Invalid state");
                }
                
                if (replyModel.Status)
                {
                    
                    Console.WriteLine($"Success: {replyModel.Content}");
                    
                    _messageQueue.Unlock();
                    await _messageQueue.DequeueMessageAsync(); 
                    
                    _protocolState = ProtocolState.Open;
                }
                else
                {
                    Console.WriteLine($"Failure: {string.Join(" ", replyModel.Content)}");
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
            await _cancellationTokenSource.CancelAsync();
        }
    }
    
    public async void OnSendingReady(object? sender, EventArgs args)
    {
        _messageQueue.Unlock();
        await _messageQueue.DequeueMessageAsync();
    }
    
    public async Task Auth(UserCommandModel command)
    {
        if (_protocolState != ProtocolState.Start)
        {
            throw new Exception("Invalid state");
        }
        
        var parts = command.Content.Split(" ");
        await _transport.Auth(new AuthModel()
            { Username = parts[0], Secret = parts[1], DisplayName = parts[2] }
        );

        _displayName = parts[2];
        _protocolState = ProtocolState.Auth;
    }
    
    public async Task Join(UserCommandModel command)
    {
        if (_protocolState != ProtocolState.Open)
        {
            throw new Exception("Invalid state");
        }
        
        var parts = command.Content.Split(" ");
        await _transport.Join(new JoinModel() { ChannelId = parts[0], DisplayName = _displayName });
    }
    
    public async Task Message(UserCommandModel command)
    {
        if (_protocolState != ProtocolState.Open)
        {
            throw new Exception("Invalid state");
        }
        
        await _transport.Message(new MessageModel() { Content = command.Content, DisplayName = _displayName });
    }

    public async Task Rename(UserCommandModel command)
    {
        var parts = command.Content.Split(" ");
        _displayName = parts[0];
    }
}