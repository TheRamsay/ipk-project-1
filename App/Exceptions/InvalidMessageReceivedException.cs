using App.Enums;
using App.Models;

namespace App.Exceptions;

public class InvalidMessageReceivedException(ProtocolState currentState) : Exception($"Invalid message received (current state: {currentState})");