using System;
using IPK.Project1.App.Enums;
using CommandLine;

namespace IPK.Project1.App;

public class Options
{
    [Option('t', Required = true, HelpText = "Transport protocol used for connection")]
    public TransportProtocol Protocol { get; set; }
    
    [Option('s', Required = true, HelpText = "Server IP or hostname")]
    public string Host { get; set; }
    
    [Option('p', Required = false, Default = 4567, HelpText = "Server port")]
    public ushort Port { get; set; }
    
    [Option('d', Required = false, Default = 3, HelpText = "UDP confirmation timeout")]
    public ushort Timeout { get; set; }
    
    [Option('r', Default = 3, Required = false, HelpText = "Maximum number of UDP retransmissions")]
    public byte Rentransmissions { get; set; }
}