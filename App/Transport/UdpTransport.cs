using System.Net.Sockets;
using System.Text;
using App.Models;
using App.Models.udp;

namespace App.Transport;

public class UdpTransport : ITransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly CancellationToken _cancellationToken;
    private readonly UdpClient _client = new();

    private int messageIdSequence = 0;
    
    public event EventHandler<IBaseModel>? OnMessage;

    public UdpTransport(string host, int port, CancellationToken cancellationToken)
    {
        _host = host;
        _port = port;
        _cancellationToken = cancellationToken;
    }

    public Task Auth(AuthModel data)
    {
        throw new NotImplementedException();
    }

    public Task Join(JoinModel data)
    {
        throw new NotImplementedException();
    }

    public Task Message(MessageModel data)
    {
        throw new NotImplementedException();
    }

    public Task Error(MessageModel data)
    {
        throw new NotImplementedException();
    }

    public Task Reply(ReplyModel data)
    {
        throw new NotImplementedException();
    }

    public Task Bye()
    {
        throw new NotImplementedException();
    }

    public async Task Start()
    {
        _client.Connect(_host, _port);
        Console.WriteLine("Connected sheeesh 🦞");
        
        while (!_cancellationToken.IsCancellationRequested)
        {
            var receiveBuffer = (await _client.ReceiveAsync(_cancellationToken)).Buffer;
            var responseData = Encoding.UTF8.GetString(receiveBuffer);
            
            Console.WriteLine($"[RECEIVED] {responseData}");
            
            // OnMessage.Invoke(this, ParseMessage(responseData));

            await Task.Delay(100, _cancellationToken);
        }
    }

    public Task Disconnect()
    {
        _client.Close();
        return Task.FromResult(0);
    }

    private async Task Send(IBaseModel data)
    {
        var buffer = new byte[1024];

        switch (data)
        {
           case UdpConfirmModel confirmModel:
               buffer[0] = (byte)confirmModel.MessageType;
               BitConverter.GetBytes(confirmModel.RefMessageId).CopyTo(buffer, 1);
               break;
           case UdpReplyModel replyModel:
               buffer[0] = (byte)replyModel.UdpMessageType;
               BitConverter.GetBytes(messageIdSequence++).CopyTo(buffer, 1);
               buffer[3] = Convert.ToByte(replyModel.Status);
               BitConverter.GetBytes(replyModel.RefMessageId).CopyTo(buffer, 4);
               Encoding.UTF8.GetBytes(replyModel.Content).CopyTo(buffer, 6);
               buffer[6 + replyModel.Content.Length + 1] = 0;
               break;
           case UdpAuthModel authModel:
               buffer[0] = (byte)authModel.UdpMessageType;
               BitConverter.GetBytes(messageIdSequence++).CopyTo(buffer, 1);
               
               Encoding.UTF8.GetBytes(authModel.Username).CopyTo(buffer, 1);
               buffer[1 + authModel.Username.Length + 1] = 0;
               
               Encoding.UTF8.GetBytes(authModel.DisplayName).CopyTo(buffer, 1);
               buffer[1 + authModel.DisplayName.Length + 1] = 0;
               
               Encoding.UTF8.GetBytes(authModel.Secret).CopyTo(buffer, 1);
               buffer[1 + authModel.Secret.Length + 1] = 0;
               break;
           case UdpJoinModel joinModel:
               buffer[0] = (byte)joinModel.MessageType;
               BitConverter.GetBytes(messageIdSequence++).CopyTo(buffer, 1);
               
               Encoding.UTF8.GetBytes(joinModel.DisplayName).CopyTo(buffer, 1);
               buffer[1 + joinModel.DisplayName.Length + 1] = 0;
               break;
        }
        
        await _client.SendAsync(buffer, _cancellationToken);
    }

    private IBaseModel ParseMessage(string data)
    {
        return null;
    }
}