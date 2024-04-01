using App.Enums;
using App.Models;

namespace App.Exceptions;

public class TransportException(string message) : Exception(message);