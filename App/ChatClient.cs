using System.ComponentModel.DataAnnotations;
using App.Enums;
using App.Exceptions;
using App.Input;
using App.Models;

namespace App;

public class ChatClient
{
    private readonly Ipk24ChatProtocol _protocol;
    public readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IStandardInputReader _standardInputReader;
    
    private string _displayName = string.Empty;
    
    public ThreadSafeBool Finished { get; set; } = new(false);

    public ChatClient(Ipk24ChatProtocol protocol, IStandardInputReader standardInputReader, CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
        _protocol = protocol;
        _standardInputReader = standardInputReader;
        
        // Event subscription
        _protocol.OnMessage += OnMessageReceived;
    }

    public async Task Start()
    {
        Task transportTask = _protocol.Start();
        Task stdinTask = ReadInputAsync();
        
        try
        {
            await await Task.WhenAny(transportTask, stdinTask);
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine("ERR: Operation cancelled.");
            return;
        }
        catch (ServerUnreachableException e)
        {
            Console.WriteLine($"ERR: {e.Message}");
            return;
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERR: {e.Message}");
            await _protocol.Disconnect();
            await _cancellationTokenSource.CancelAsync();
            return;
        }

        if (!Finished.Value)
        {
            Finished.Value = true;
            await _protocol.Disconnect();
            await _cancellationTokenSource.CancelAsync();
        }
    }
    
    public async Task Stop()
    {
        Console.WriteLine("Disconnecting...");
        await _protocol.Disconnect();
        Console.WriteLine("Cancelling the token...");
        await _cancellationTokenSource.CancelAsync();
    }
    
    private async Task ReadInputAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            var line = _standardInputReader.ReadLine();

            // Eof reached
            if (line is null)
            {
                // await Stop();
                Console.WriteLine("EOF reached.");
                return;
            }

            // Empty line not allowed
            if (line.Length == 0)
            {
                Console.WriteLine("ERR: Messages can't be empty.");
            }

            try
            {
                var command = UserCommandModel.ParseCommand(line);
                await ProcessCommand(command);
            }
            catch (InvalidInputException e)
            {
                Console.WriteLine($"ERR: {e.Message}");
            }
            catch (ValidationException e)
            {
                Console.WriteLine($"ERR: Invalid format, try /help to see the correct format (Reason: {e.Message})");
            }
        }
        
        Console.WriteLine("Exiting loop...");
    }

    private async Task ProcessCommand(UserCommandModel command)
    {
        IBaseModel model;
        switch (command.Command)
        {
            case UserCommand.Auth:
                model = AuthModel.Parse(command.Content);
                _displayName = ((AuthModel) model).DisplayName;
                await _protocol.Send(model);
                break;
            case UserCommand.Join:
                model = JoinModel.Parse(command.Content);
                await _protocol.Send(model);
                break;
            case UserCommand.Message:
                model = MessageModel.Parse(command.Content);
                ((MessageModel) model).DisplayName = _displayName;
                await _protocol.Send(model);
                break;
            case UserCommand.Rename:
                model = RenameModel.Parse(command.Content);
                _displayName = ((RenameModel) model).DisplayName;
                break;
            default:
                throw new ValidationException("Invalid command, try /help to see the correct format.");
        }
    }

    private void OnMessageReceived(object? sender, IBaseModel model)
    {
        if (model is MessageModel message)
        {
            Console.WriteLine($"{message.DisplayName}: {message.Content}");
        } else if (model is ReplyModel reply)
        {
            Console.WriteLine(reply.Status ? $"Success: {reply.Content}" : $"Failure: {reply.Content}");
        } else if (model is ErrorModel error)
        {
            Console.WriteLine($"ERR FROM: {error.Content}");
        }
    }
}