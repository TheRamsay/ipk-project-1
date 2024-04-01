using System.ComponentModel.DataAnnotations;
using App.Models;
using Xunit;

namespace App.Tests;

public class JoinModelTests
{
    
    [Fact]
    public void JoinModel_Valid()
    {
        // Arrange
        var model = new JoinModel()
        {
            DisplayName = "Pepa_z_Brna",
            ChannelId = "general"
        };
        
        // Act & Assert
        var exception = Record.Exception(() => ModelValidator.Validate(model));
        Assert.Null(exception);
    } 
    
    [Fact]
    public void JoinModel_DisplayNameTooLong()
    {
        // Arrange
        var model = new JoinModel()
        {
            DisplayName = "a".PadRight(129, 'a'),
            ChannelId = "general"
        };
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    } 
    
    [Fact]
    public void JoinModel_DisplayNameInvalidCharacters()
    {
        // Arrange
        var model = new JoinModel()
        {
            // DisplayName doesn't allow diacritics
            DisplayName = "Pepík_z_Brna",
            ChannelId = "general"
        };
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    } 
    
    [Fact]
    public void JoinModel_ChannelIdTooLong()
    {
        // Arrange
        var model = new JoinModel()
        {
            DisplayName = "Pepa_z_Brna",
            ChannelId = "a".PadRight(129, 'a')
        };
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    } 
    
    [Fact]
    public void JoinModel_ChannelIdInvalidCharacters()
    {
        // Arrange
        var model = new JoinModel()
        {
            // DisplayName doesn't allow diacritics
            DisplayName = "Pepík_z_Brna",
            ChannelId = "muj general"
        };
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    } 
    
    [Fact]
    public void JoinModel_CommandValid()
    {
        // Arrange
        var data = "general";
        
        // Act
        var model = JoinModel.Parse(data);
        
        // Assert
        Assert.Equal(data, model.ChannelId);
    }
    
    [Fact]
    public void JoinModel_CommandInvalid()
    {
        // Arrange
        var data = "muj general";
        
        // Act & Assert
        Assert.Throws<ValidationException>(() => AuthModel.Parse(data));
    }
}