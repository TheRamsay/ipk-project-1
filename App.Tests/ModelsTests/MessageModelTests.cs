using System.ComponentModel.DataAnnotations;
using App.Models;
using Xunit;

namespace App.Tests;

public class MessageModelTests
{
    
    [Fact]
    public void AuthModel_Valid()
    {
        // Arrange
        var model = new MessageModel()
        {
            DisplayName = "Pepa_z_Brna",
            Content = "AHOJ ja jsem Pepa"
        };
        
        // Act & Assert
        var exception = Record.Exception(() => ModelValidator.Validate(model));
        Assert.Null(exception);
    } 
    
    [Fact]
    public void MessageModel_DisplayNameTooLong()
    {
        // Arrange
        var model = new MessageModel()
        {
            DisplayName = "a".PadRight(129, 'a'),
            Content = "AHOJ ja jsem Pepa"
        };
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    } 
    
    [Fact]
    public void MessageModel_DisplayNameInvalidCharacters()
    {
        // Arrange
        var model = new MessageModel()
        {
            // DisplayName doesn't allow diacritics
            DisplayName = "Pepík_z_Brna",
            Content = "AHOJ ja jsem Pepa"
        };
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    } 
    
    [Fact]
    public void MessageModel_ContentTooLong()
    {
        // Arrange
        var model = new MessageModel()
        {
            DisplayName = "Pepa_z_Brna",
            Content = "a".PadRight(1403, 'a')
        };
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    } 
    
    [Fact]
    public void MessageModel_ContentInvalidCharacters()
    {
        // Arrange
        var model = new MessageModel()
        {
            // DisplayName doesn't allow diacritics
            DisplayName = "Pepík_z_Brna",
            Content = "AHOJ ja jsem Pepa 👋👋"
        };
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    } 
    
    [Fact]
    public void MessageModel_CommandValid()
    {
        // Arrange
        var data = "Ahoj jak se mas";
        
        // Act
        var model = MessageModel.Parse(data);
        
        // Assert
        Assert.Equal(data, model.Content);
    }
    
    [Fact]
    public void MessageModel_CommandInvalid()
    {
        // Arrange
        var data = "Ahoj jak se mas 👋";
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => AuthModel.Parse(data));
    }
}