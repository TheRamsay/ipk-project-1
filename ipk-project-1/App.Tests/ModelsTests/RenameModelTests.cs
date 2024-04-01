using System.ComponentModel.DataAnnotations;
using App.Models;
using App.Transport;
using Xunit;
using NSubstitute;

namespace App.Tests;

public class RenameModelTests
{
    [Fact]
    public void RenameModel_Valid()
    {
        // Arrange
        var model = new RenameModel()
        {
            DisplayName = "Pepa_z_Brna",
        };
        
        // Act & Assert
        var exception = Record.Exception(() => ModelValidator.Validate(model));
        Assert.Null(exception);
    } 
    
    [Fact]
    public void RenameModel_DisplayNameTooLong()
    {
        // Arrange
        var model = new RenameModel()
        {
            DisplayName = "a".PadRight(129, 'a'),
        };
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    } 
    
    [Fact]
    public void RenameModel_DisplayNameInvalidCharacters()
    {
        // Arrange
        var model = new RenameModel()
        {
            // DisplayName doesn't allow diacritics
            DisplayName = "Pepík_z_Brna",
        };
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    } 
    
    [Fact]
    public void RenameModel_CommandValid()
    {
        // Arrange
        var data = "Pepa_z_Brna";
        
        // Act
        var model = RenameModel.Parse(data);
        
        // Assert
        Assert.Equal("Pepa_z_Brna", model.DisplayName);
    }
    
    [Fact]
    public void RenameModel_CommandInvalid()
    {
        // Arrange
        var data = "lól XD omegalul";
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => RenameModel.Parse(data));
    }
}