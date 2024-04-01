using App.Enums;
using App.Input;
using App.Models;
using App.Transport;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Xunit;

namespace App.Tests;

public class ProtocolTests
{
    [Fact]
    public async Task Auth()
    {
        // Arrange
        // var source = new CancellationTokenSource();
        //
        // var transport = Substitute.For<ITransport>();
        //
        // transport.When(transport => transport.Disconnect()).Do(_ =>
        //     {
        //         source.Cancel();
        //         transport.Disconnect();
        // });
        //
        // transport
        //     .Start(Arg.Any<ProtocolStateBox>())
        //     .Returns(Task.Run(() => Task.Delay(Timeout.Infinite)))
        //     .AndDoes(_ => transport.OnConnected += Raise.Event<EventHandler>());
        //
        // transport
        //     .Auth(Arg.Any<AuthModel>())
        //     .Returns(Task.CompletedTask)
        //     .AndDoes(_ =>
        //     {
        //         transport.OnMessageReceived += Raise.Event<EventHandler<IBaseModel>>(new object(), new ReplyModel()
        //         {
        //             Status = true,
        //             Content = "Welcome!"
        //         });
        //     });
        //
        // var protocol = new Ipk24ChatProtocol(transport, source);
        //
        // protocol.OnConnected += async (_, _) =>
        // {
        //     await protocol.Send(new AuthModel
        //     {
        //         DisplayName = "Pepa_z_Brna",
        //         Username = "pepa",
        //         Secret = "heslo"
        //     });
        // };
        //
        // protocol.OnMessage += async (_, msg) =>
        // {
        //     Assert.IsType<ReplyModel>(msg);
        //     Assert.Equal("Welcome!", ((ReplyModel)msg).Content);
        //     Assert.True(((ReplyModel)msg).Status);
        //     source.Cancel();
        //     await protocol.Disconnect();
        // };
        //
        // Task.Run(async() =>
        // {
        //     await Task.Delay(1500);
        //     await protocol.Disconnect();
        //     throw new TimeoutException("Test timed out. No response from the server.");
        // });
        //
        // await protocol.Start();
    }
}
