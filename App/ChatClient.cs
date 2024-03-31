using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
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
    private readonly SemaphoreSlim _connectedSignal = new (0, 1);
    private string _displayName = string.Empty;
    
    public ThreadSafeBool Finished { get; set; } = new(false);

    public ChatClient(Ipk24ChatProtocol protocol, IStandardInputReader standardInputReader, CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
        _protocol = protocol;
        _standardInputReader = standardInputReader;
        
        _protocol.OnMessage += OnMessageReceived;
        _protocol.OnConnected += OnConnected;
    }

    public async Task Start()
    {
        var transportTask = _protocol.Start();
        var stdinTask = ReadInputAsync();
        var statusCode = 0;

        try
        {
            await await Task.WhenAny(transportTask, stdinTask);
        }
        catch (OperationCanceledException e)
        {
            ClientLogger.LogInternalError("Operation cancelled.");
            return;
        }
        catch (SocketException e)
        {
            ClientLogger.LogInternalError("Operation cancelled.");
            return;
        }
        catch (IOException e)
        {
            ClientLogger.LogInternalError("Operation cancelled.");
            return;
        }
        catch (ServerUnreachableException e)
        {
            ClientLogger.LogInternalError(e.Message);
            return;
        }
        catch (ServerException e)
        {
            statusCode = 1;
            ClientLogger.LogError(e.ErrorData);
        }
        catch (Exception e)
        {
            statusCode = 1;
            ClientLogger.LogInternalError(e.Message);
        }

        if (!Finished.Value)
        {
            Finished.Value = true;
            await _protocol.Disconnect();
            await _cancellationTokenSource.CancelAsync();
        }
        
        Environment.Exit(statusCode);
    }

    public async Task Stop()
    {
        ClientLogger.LogDebug("Disconnecting...");
        await _protocol.Disconnect();
    }

    private async Task ReadInputAsync()
    {
        await _connectedSignal.WaitAsync();
        
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            var line = _standardInputReader.ReadLine();

            // Eof reached
            if (line is null)
            {
                // await Stop();
                ClientLogger.LogDebug("EOF reached");
                return;
            }

            // Empty line not allowed
            if (line.Length == 0)
            {
                ClientLogger.LogInternalError("Messages can't be empty.");
            }

            try
            {
                var command = UserCommandModel.ParseCommand(line);
                await ProcessCommand(command);
            }
            catch (InvalidInputException e)
            {
                ClientLogger.LogInternalError($"{e.Message}");
            }
            catch (ValidationException e)
            {
                ClientLogger.LogInternalError($"Invalid format, try /help to see the correct format (Reason: {e.Message})");
            }
        }
    }

    private async Task ProcessCommand(UserCommandModel command)
    {
        IBaseModel model;
        switch (command.Command)
        {
            case UserCommand.Auth:
                model = AuthModel.Parse(command.Content);
                _displayName = ((AuthModel)model).DisplayName;
                await _protocol.Send(model);
                break;
            case UserCommand.Join:
                model = JoinModel.Parse(command.Content);
                ((JoinModel)model).DisplayName = _displayName;
                await _protocol.Send(model);
                break;
            case UserCommand.Message:
                model = MessageModel.Parse(command.Content);
                ((MessageModel)model).DisplayName = _displayName;
                await _protocol.Send(model);
                break;
            case UserCommand.Rename:
                model = RenameModel.Parse(command.Content);
                ClientLogger.LogDebug($"Renamed to {((RenameModel)model).DisplayName}");
                _displayName = ((RenameModel)model).DisplayName;
                ClientLogger.LogDebug($"New name is {_displayName}");
                break;
            default:
                throw new ValidationException("Invalid command, try /help to see the correct format.");
        }
    }

    private void OnMessageReceived(object? sender, IBaseModel model)
    {
        // throw new Exception("ZMRD MFEKFWEE");
        if (model is MessageModel message)
        {
            ClientLogger.LogMessage(message);
        }
        else if (model is ReplyModel reply)
        {
            ClientLogger.LogReploy(reply);
        }
        else if (model is ErrorModel error)
        {
            ClientLogger.LogError(error);
        }
    }

    private void OnConnected(object? sender, EventArgs args)
    {
        _connectedSignal.Release();
    }
}