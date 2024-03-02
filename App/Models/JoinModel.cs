using System.ComponentModel.DataAnnotations;

namespace App.Models;

public class JoinModel : BaseModel
{
    
    [RegularExpression("[A-z0-9-]{1, 20}", ErrorMessage = "ChannelId has to be alphanumerical with length from 1 to 20 characters")]
    public required string ChannelId { get; set; }
    
    [RegularExpression("[0x21-7E]{1, 20}", ErrorMessage = "DisplayName has to have printable characters with length from 1 to 128 characters")]
    public required string DisplayName { get; set; }
    
}