namespace App.Enums;

public enum StateTransitionType
{
    None,
    Any,
    Msg,
    Err,
    Bye,
    Auth,
    Join,
    ReplyOk,
    ReplyErr,
    Reply
}