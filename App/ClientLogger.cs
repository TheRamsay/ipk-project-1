using App.Models;

namespace App;

public static class ClientLogger
{
    public static void LogInternalError(string msg)
    {
        Console.Error.WriteLine($"ERR: {msg}");
    }

    public static void LogError(ErrorModel model)
    {
        Console.Error.WriteLine($"ERR FROM {model.DisplayName}: {model.Content}");
    }

    public static void LogMessage(MessageModel model)
    {
        Console.WriteLine($"{model.DisplayName}: {model.Content}");
    }

    public static void LogReploy(ReplyModel model)
    {
        var status = model.Status ? "Success" : "Failure";
        Console.Error.WriteLine($"{status}: {model.Content}");
    }

    public static void LogDebug(string msg)
    {
        Console.WriteLine($"[DEBUG] {msg}");
    }
}