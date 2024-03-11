using App.Enums;
using App.Models;

namespace App.Transport;

public interface ITransport
{
    public event EventHandler<IBaseModel> OnMessage;
    
    public Task Auth(AuthModel data);
    public Task Join(JoinModel data);
    public Task Message(MessageModel data);
    public Task Error(MessageModel data);
    public Task Reply(ReplyModel data);
    public Task Bye();
    public Task Start(ProtocolState protocolState);
    public Task Disconnect();
}