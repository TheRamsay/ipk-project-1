using App.Enums;

namespace App.Models;

public class ReplyModel : IBaseModel
{
    public required bool Status { get; set; }
    public required string Content { get; set; }
}