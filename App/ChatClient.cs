using App.Enums;
using App.Models;
using App.Models.udp;
using App.Transport;

namespace App;

public class ChatClient
{
    private readonly ITransport _transport;
    private readonly CancellationTokenSource _cancellationTokenSource;
    
    private ProtocolState _protocolState;
    private string _displayName = String.Empty;

    private ReplyLock _authLock = new("Waiting for a auth confirmation from the server.");
    private ReplyLock _joinLock = new("Waiting a join confirmation from the server.");

    private string _authLockString = "Waiting for a auth confirmation from the server.";
    private string _joinLockString = "Waiting a join confirmation from the server.";

    public ChatClient(ITransport transport, CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
        _transport = transport;
        
        _transport.OnMessage += OnMessageReceived;
        
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
            var line = Console.ReadLine();

            if (_protocolState == ProtocolState.End)
            {
                continue;
            }

            if (line is null)
            {
                if (_protocolState is ProtocolState.Auth or ProtocolState.Error)
                {
                    await _transport.Disconnect();
                    return;
                }
                
                throw new Exception("Invalid state");
            }

            if (line.Length == 0)
            {
                throw new Exception("Messages can't be empty.");
            }

            while (_authLock.IsLocked || _joinLock.IsLocked)
            {
                if (_authLock.IsLocked)
                {
                    Console.WriteLine(_authLock.InfoMessage);
                }
                else
                {
                    Console.WriteLine(_joinLock.InfoMessage);
                }
                await Task.Delay(100);
            }
            
            var command = UserCommandModel.ParseCommand(line);
            string[] parts;
            
            switch (command.Command)
            {
                case UserCommand.Auth:
                    if (_protocolState == ProtocolState.Start)
                    {
                        parts = command.Content.Split(" ");
                        await _transport.Auth(new AuthModel()
                            { Username = parts[1], Secret = parts[2], DisplayName = parts[3] }
                        );

                        _authLock.Lock();
                        _displayName = parts[3];
                        _protocolState = ProtocolState.Auth;
                        break;
                    }
                    
                    throw new Exception("Invalid state");
                case UserCommand.Join:
                    if (_protocolState == ProtocolState.Open)
                    {
                        parts = command.Content.Split(" ");
                        await _transport.Join(new JoinModel() { ChannelId = parts[1], DisplayName = _displayName});
                        _joinLock.Lock();
                        break;
                    }
                    
                    throw new Exception("Invalid state");
                case UserCommand.Rename:
                    parts = command.Content.Split(" ");
                    _displayName = parts[1];
                    break;
                case UserCommand.Help:
                    Console.WriteLine("😼😼😼😼😼😼");
                    break;
                case UserCommand.End:
                    await _transport.Bye();
                    await _cancellationTokenSource.CancelAsync();
                    break;
                case UserCommand.Message:
                    if (_protocolState == ProtocolState.Open)
                    {
                        await _transport.Message(new MessageModel() { Content = line, DisplayName = _displayName});
                    }
                    else
                    { 
                        throw new Exception("Invalid state");
                    }

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
                    if (_protocolState is ProtocolState.Auth)
                    {
                        _authLock.Unlock();
                    }
                    else
                    {
                        _joinLock.Unlock();
                    }
                    
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
    
    public UserCommand ParseCommand(string command)
    {
        return command switch
        {
            "/auth" => UserCommand.Auth,
            "/join" => UserCommand.Join,
            "/rename" => UserCommand.Rename,
            "/end" => UserCommand.End,
            var s when s.StartsWith("/") => throw new Exception("Invalid command"),
            _ => UserCommand.Message
        };
    }
}