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

    private ReplyLock _authLock = new ReplyLock("Waiting for a auth confirmation from the server.");
    private ReplyLock _joinLock = new ReplyLock("Waiting a join confirmation from the server.");

    public ChatClient(ITransport transport, CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
        _transport = transport;
        
        _transport.OnMessage += OnMessageReceived;
        
        _protocolState = ProtocolState.Start;
    }

    public async Task Start()
    {
        Task transportTask = _transport.Start();
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
            
            if (line.StartsWith('/'))
            {
                var parts = line.Split(" ");
                var command = parts[0].Substring(1, parts[0].Length - 1);

                switch (command)
                {
                    case "auth":
                        if (_protocolState == ProtocolState.Start)
                        {
                            await _transport.Auth(new AuthModel()
                                { Username = parts[1], Secret = parts[2], DisplayName = parts[3] }
                            );

                            _authLock.Lock();
                            _displayName = parts[3];
                            _protocolState = ProtocolState.Auth;
                            break;
                        }
                        
                        throw new Exception("Invalid state");
                    case "join":
                        if (_protocolState == ProtocolState.Open)
                        {
                            await _transport.Join(new JoinModel() { ChannelId = parts[1], DisplayName = _displayName});
                            _joinLock.Lock();
                            break;
                        }
                        
                        throw new Exception("Invalid state");
                    case "rename":
                        _displayName = parts[1];
                        break;
                    case "help":
                        Console.WriteLine("😼😼😼😼😼😼");
                        break;
                    case "end":
                        await _transport.Bye();
                        await _cancellationTokenSource.CancelAsync();
                        break;
                    default:
                        throw new Exception("Invalid state");
                }
            }
            else
            {
                if (_protocolState == ProtocolState.Open)
                {
                    await _transport.Message(new MessageModel() { Content = line, DisplayName = _displayName});
                }
                else
                { 
                    throw new Exception("Invalid state");
                }
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
}