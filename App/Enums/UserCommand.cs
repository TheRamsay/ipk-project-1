﻿namespace App.Enums;

public enum UserCommand
{
    Auth,
    Join,
    Rename,
    Message,
    End,
    Help
}

public record UserCommandModel(UserCommand Command, string Content)
{
    public static UserCommandModel ParseCommand(string command)
    {
        return command.Split(" ")[0] switch
        {
            "/auth" => new UserCommandModel(UserCommand.Auth, GetContent(command.Split(" "))),
            "/join" => new UserCommandModel(UserCommand.Join, GetContent(command.Split(" "))),
            "/rename" => new UserCommandModel(UserCommand.Rename, GetContent(command.Split(" "))),
            // TODO: end is not a real command
            "/end" => new UserCommandModel(UserCommand.End, GetContent(command.Split(" "))),
            "/help" => new UserCommandModel(UserCommand.Help, GetContent(command.Split(" "))),
            _ => new UserCommandModel(UserCommand.Message, command)
        };

        string GetContent(string[] parts) => string.Join(" ", parts[1..]);
    }
}

