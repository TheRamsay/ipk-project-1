using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net.Sockets;
using App.Enums;
using App.Exceptions;
using App.Input;
using App.Models;

namespace App;

public class ChatClient
{
    private readonly IProtocol _protocol;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IStandardInputReader _standardInputReader;
    private readonly SemaphoreSlim _connectedSignal = new(0, 1);
    
    private string _displayName = string.Empty;

    public ThreadSafeBool Finished { get; set; } = new(false);

    public ChatClient(IProtocol protocol, IStandardInputReader standardInputReader, CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
        _protocol = protocol;
        _standardInputReader = standardInputReader;

        _protocol.OnMessage += OnMessageReceivedHandler;
        _protocol.OnConnected += OnConnectedHandler;
    }

    public async Task<int> Start()
    {
        var transportTask = _protocol.Start();
        var stdinTask = ReadInputAsync();
        var statusCode = 0;

        try
        {
            await await Task.WhenAny(transportTask, stdinTask);
        }
        // Catching OperationCanceledException to handle the cancellation token
        catch (OperationCanceledException)
        {
            ClientLogger.LogInternalError("Operation cancelled.");
        }
        // If we can't reach the server, we want to log the error and exit, no disconnect needed
        catch (Exception e) when (e is ServerUnreachableException or SocketException)
        {
            statusCode = 1;
            Finished.Value = true;
            ClientLogger.LogInternalError(e.Message);
        }
        // Exception received from the server, we want to log the error differently
        catch (ServerException e)
        {
            statusCode = 1;
            ClientLogger.LogError(e.ErrorData);
        }
        // Other internal error
        catch (Exception e)
        {
            statusCode = 1;
            ClientLogger.LogInternalError(e.Message);
        }
        finally
        {
            // Check if client wasn't finished yet from SIGINT handler
            // Finished.Value is thread safe, it's using a lock internally
            if (!Finished.Value)
            {
                Finished.Value = true;
                await _protocol.Disconnect();
                // Just to make sure 😏
                await _cancellationTokenSource.CancelAsync();
            }
        }
        
        return statusCode;
    }

    public async Task Stop()
    {
        ClientLogger.LogDebug("Disconnecting...");
        await _protocol.Disconnect();
        await _cancellationTokenSource.CancelAsync();
    }

    private async Task ReadInputAsync()
    {
        // Wait until the client is connected to the server
        await _connectedSignal.WaitAsync(_cancellationTokenSource.Token);

        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            var line = _standardInputReader.ReadLine();

            // Eof reached
            if (line is null)
            {
                ClientLogger.LogDebug("EOF reached");
                return;
            }

            // Empty line not allowed
            if (line.Length == 0)
            {
                ClientLogger.LogInternalError("Messages can't be empty.");
                continue;
            }

            try
            {
                var command = UserCommandModel.ParseCommand(line);
                await ProcessCommand(command);
            }
            // User action is invalid, we don't want to crash the client, just log the error
            catch (InvalidInputException e)
            {
                ClientLogger.LogInternalError($"{e.Message}");
            }
            // User input is invalid, we don't want to crash the client, just log the error
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
            case UserCommand.Help:
                Program.PrintHelp();
                break;
            default:
                throw new ValidationException("Invalid command, try /help to see the correct format.");
        }
    }

    private void OnMessageReceivedHandler(object? sender, IBaseModel model)
    {
        switch (model)
        {
            case MessageModel message:
                ClientLogger.LogMessage(message);
                break;
            case ReplyModel reply:
                ClientLogger.LogReploy(reply);
                break;
        }
    }

    private void OnConnectedHandler(object? sender, EventArgs args)
    {
        _connectedSignal.Release();
    }
}