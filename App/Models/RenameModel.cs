using System.ComponentModel.DataAnnotations;
using App.Enums;

namespace App.Models;

public class RenameModel : IBaseModel
{
    [RegularExpression("[0x21-7E]{1, 20}", ErrorMessage = "DisplayName has to have printable characters with length from 1 to 128 characters")]
    public required string DisplayName { get; set; }
}