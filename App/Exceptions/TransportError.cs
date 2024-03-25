using App.Enums;
using App.Models;

namespace App.Exceptions;

public class TransportError(string message) : Exception(message);