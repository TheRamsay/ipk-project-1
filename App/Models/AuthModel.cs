using System.ComponentModel.DataAnnotations;
using App.Enums;

namespace App.Models;

public class AuthModel : IBaseModel
{
    [RegularExpression("[A-z0-9-]{1, 20}", ErrorMessage = "Username has to be alphanumerical with length from 1 to 20 characters")]
    public string Username { get; set; }
    
    [RegularExpression("[0x21-7E]{1, 20}", ErrorMessage = "DisplayName has to have printable characters with length from 1 to 128 characters")]
    public string DisplayName { get; set; }
    
    [RegularExpression("[A-z0-9-]{1, 128}", ErrorMessage = "Secret has to be alphanumerical with length from 1 to 128 characters")]
    public string Secret { get; set; }
}