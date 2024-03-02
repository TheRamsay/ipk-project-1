namespace App.Models;

public class ReplyModel : BaseModel
{
    public required bool Status { get; set; }
    public required string Content { get; set; }
}