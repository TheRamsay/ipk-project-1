using App.Enums;
using App.Models;

namespace App.Exceptions;

public class InternalErrorException(string message) : Exception(message);