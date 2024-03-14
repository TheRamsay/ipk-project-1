using App.input;
using App.Transport;
using Xunit;
using NSubstitute;

namespace App.Tests;

public class ChatClientTests
{
    [Fact]
    public async void AuthTest()
    {
        var standardInputReader = Substitute.For<IStandardInputReader>();
        standardInputReader.ReadLine().Returns("/auth xlogin00 123456 pepik", "/end");
        
        var transport = Substitute.For<ITransport>();

        var cancellationTokenSource = new CancellationTokenSource();
        var client = new ChatClient(transport, standardInputReader, cancellationTokenSource);
        await client.Start();
    } 
}