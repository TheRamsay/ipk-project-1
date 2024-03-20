using App.Enums;
using App.Exceptions;
using App.input;
using App.Models;

namespace App;

public class ChatClient
{
    private readonly Ipk24ChatProtocol _protocol;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IStandardInputReader _standardInputReader;
    
    private string _displayName = string.Empty;

    public ChatClient(Ipk24ChatProtocol protocol, IStandardInputReader standardInputReader, CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
        _protocol = protocol;
        _standardInputReader = standardInputReader;
        
        // Event subscription
        _protocol.OnMessage += OnMessageReceived;
        _protocol.OnError += OnError;
    }

    public async Task Start()
    {
        Task transportTask = _protocol.Start();
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
            // await _protocol.Disconnect();
            await _cancellationTokenSource.CancelAsync();
        }
    }
    
    private async Task ReadInputAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            var line = _standardInputReader.ReadLine();

            // Eof reached
            if (line is null)
            {
                await _protocol.Disconnect();
                return;
            }

            // Empty line not allowed
            if (line.Length == 0)
            {
                throw new Exception("Messages can't be empty.");
            }

            var command = UserCommandModel.ParseCommand(line);
            try
            {

            }
            catch (InvalidInputException e)
            {
                Console.WriteLine($"ERROR: {e.Message}");
            }
            catch (Exception e)
            {
                throw;
            }

            await Task.Delay(100);
        }
    }

    private async Task ProcessCommand(UserCommandModel command)
    {
        string[] parts;
        switch (command.Command)
        {
            case UserCommand.Auth:
                parts = command.Content.Split(" ");
                await _protocol.Send(new AuthModel { Username = parts[0], Secret = parts[1], DisplayName = parts[2] });
                break;
            case UserCommand.Join:
                parts = command.Content.Split(" ");
                await _protocol.Send(new JoinModel { ChannelId = parts[0], DisplayName = _displayName });
                break;
            case UserCommand.Message:
                await _protocol.Send(new MessageModel { Content = command.Content, DisplayName = _displayName });
                break;
            case UserCommand.Rename:
                _displayName = command.Content;
                break;
            default:
                throw new Exception("Invalid command");
        }
    }

    private void OnMessageReceived(object? sender, IBaseModel model)
    {
        if (model is MessageModel message)
        {
            Console.WriteLine($"{message.DisplayName}: {message.Content}");
        } else if (model is ReplyModel reply)
        {
            Console.WriteLine($"Reply: {reply.Content}");
        }
    }

    private void OnError(object? sender, Exception e)
    {
        Console.WriteLine($"ERROR: {e}");
    }
}