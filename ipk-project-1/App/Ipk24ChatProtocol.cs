using App.Enums;
using App.Exceptions;
using App.Models;
using App.Transport;

namespace App;

public class Ipk24ChatProtocol: IProtocol
{
    private readonly ITransport _transport;
    // _messageDeliveredSignal is used for waiting for the message to be delivered (confirmed by the server)
    private readonly SemaphoreSlim _messageDeliveredSignal = new(0, 1);
    // _messageProcessedSignal is used for waiting for the message to be processed (for example, receiving a reply to the AUTH message)
    private readonly SemaphoreSlim _messageProcessedSignal = new(0, 1);
    // _endSignal is used for throwing exceptions from EventHandlers
    private readonly SemaphoreSlim _endSignal = new(0, 1);
    // Used for cancelling the message receive loop
    private readonly CancellationTokenSource _cancellationTokenSource;

    private Exception? _exceptionToThrow;
    private ProtocolStateBox? _protocolState;
    // TODO: ⚠️
    private const string DisplayName = "Pepa";

    public event EventHandler<IBaseModel>? OnMessage;
    public event EventHandler? OnConnected;

    public Ipk24ChatProtocol(ITransport transport, CancellationTokenSource cancellationTokenSource)
    {
        _transport = transport;
        _cancellationTokenSource = cancellationTokenSource;

        // Event subscription
        _transport.OnMessageReceived += OnMessageReceivedHandler;
        _transport.OnMessageDelivered += OnMessageDeliveredHandler;
        _transport.OnConnected += OnConnectedHandler;
    }

    public async Task Start()
    {
        _protocolState = new ProtocolStateBox(ProtocolState.Start);

        try
        {
            // Start the receive loop
            // This will end if any of the following happens:
            // - Server sends a malformed message
            // - Server sends a BYE message
            // - Server closes the connection
            // - Server sends an error message
            // - Server sends a message that is not expected in the current state
            // The ProtocolEnHandler function is used for throwing exceptions from EventHandlers
            // This is necessary because the exceptions thrown in EventHandlers are not caught by the try-catch block
            await await Task.WhenAny(_transport.Start(_protocolState), ProtocolEndHandler());
        }
        // If server sends a malformed message, send ERR, BYE and disconnect
        catch (InvalidMessageReceivedException e)
        {
            var errorModel = new ErrorModel
            {
                Content = e.Message,
                DisplayName = DisplayName
            };

            await SendInternal(_transport.Error(errorModel));
            // Exception is rethrown for proper ending of the protocol in the ChatClient
            throw;
        }
    }

    private async Task Auth(AuthModel data)
    {
        await SendInternal(_transport.Auth(data), true);
    }

    private async Task Join(JoinModel data)
    {
        await SendInternal(_transport.Join(data), true);
    }

    private async Task Message(MessageModel data)
    {
        await SendInternal(_transport.Message(data));
    }

    public async Task Disconnect()
    {
        await SendInternal(_transport.Bye());
        // This will cancel the message receive loop
        await _cancellationTokenSource.CancelAsync();
        _transport.Disconnect();
    }

    private async Task WaitForDelivered(Task task)
    {
        var tasks = new[] { _messageDeliveredSignal.WaitAsync(_cancellationTokenSource.Token), task };
        await Task.WhenAll(tasks);
    }

    private async Task WaitForDeliveredAndProcessed(Task task)
    {
        var tasks = new[] { _messageDeliveredSignal.WaitAsync(_cancellationTokenSource.Token), _messageProcessedSignal.WaitAsync(_cancellationTokenSource.Token), task };
        await Task.WhenAll(tasks);
    }

    private async Task ProtocolEndHandler()
    {
        ClientLogger.LogDebug("Waiti_canceng for end signal");
        await _endSignal.WaitAsync(_cancellationTokenSource.Token);
        ClientLogger.LogDebug("Received end signal");

        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }
    }

    private async Task SendInternal(Task task, bool waitForProcessed = false)
    {
        // If message needs to be processed, wait for the message to be delivered and processed
        // This is for example needed when sending an AUTH message, because we need to know if the server accepted it
        if (waitForProcessed)
        {
            await WaitForDeliveredAndProcessed(task);
        }
        // If the message does not need to be processed, wait only for the message to be delivered
        // In case of TCP, this will go through immediately, because the message is sent immediately
        // And the underlying transport layer will handle it for us
        // But in case of UDP, we need to wait for the message to be delivered (this is verified by receiving the CONFIRM message)
        else
        {
            await WaitForDelivered(task);
        }
    }

    public async Task Send(IBaseModel model)
    {
        if (_protocolState is null)
        {
            throw new InternalException("Protocol not started");
        }

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


    private void Receive(IBaseModel model)
    {
        if (_protocolState is null)
        {
            throw new InternalException("Protocol not started");
        }

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
                _exceptionToThrow = new ServerException(data);
                _protocolState.SetState(ProtocolState.End);
                break;
            case (ProtocolState.Open, MessageModel data):
                OnMessage?.Invoke(this, data);
                break;
            case (ProtocolState.Open, ReplyModel data):
                // We are currently not waiting for any reply, so we can ignore it
                // Unexpected reply's are valid but ignored
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
                _exceptionToThrow = new InvalidMessageReceivedException($"No action for {model} in state {_protocolState.State}");
                _protocolState.SetState(ProtocolState.End);
                break;
        }

        if (_protocolState.State == ProtocolState.End)
        {
            _endSignal.Release();
        }
    }

    #region Event handlers

    private void OnMessageDeliveredHandler(object? sender, EventArgs args)
    {
        try
        {
            _messageDeliveredSignal.Release();
        }
        catch (Exception e)
        {
            _exceptionToThrow = e;
            _endSignal.Release();
        }
    }

    private void OnMessageReceivedHandler(object? sender, IBaseModel model)
    {
        try
        {
            Receive(model);
        }
        catch (Exception e)
        {
            _exceptionToThrow = e;
            _endSignal.Release();
        }
    }

    private void OnConnectedHandler(object? sender, EventArgs args)
    {
        try
        {
            OnConnected?.Invoke(sender, args);
        }
        catch (Exception e)
        {
            _exceptionToThrow = e;
            _endSignal.Release();
        }
    }

    #endregion
}