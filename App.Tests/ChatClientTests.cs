using App.input;
using App.Models;
using App.Transport;
using Xunit;
using NSubstitute;

namespace App.Tests;

public class ChatClientTests
{
    [Fact]
    public async void AuthTest()
    {
        var transport = Substitute.For<ITransport>();
        transport.Auth(Arg.Any<AuthModel>()).Returns(Task.CompletedTask);
        
        transport.OnMessageDelivered += Raise.Event();
        
        var protocol = new Ipk24ChatProtocol(transport);
        
        await protocol.Send(Arg.Any<AuthModel>());
        
    } 
}