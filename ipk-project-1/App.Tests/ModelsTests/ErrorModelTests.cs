using System.ComponentModel.DataAnnotations;
using App.Models;
using Xunit;

namespace App.Tests;

public class ErrorModelTests
{

    [Fact]
    public void ErrorModel_Valid()
    {
        // Arrange
        var model = new ErrorModel()
        {
            DisplayName = "Pepa_z_Brna",
            Content = "AHOJ ja jsem Pepa"
        };

        // Act & Assert
        var exception = Record.Exception(() => ModelValidator.Validate(model));
        Assert.Null(exception);
    }

    [Fact]
    public void ErrorModel_DisplayNameTooLong()
    {
        // Arrange
        var model = new ErrorModel()
        {
            DisplayName = "a".PadRight(129, 'a'),
            Content = "AHOJ ja jsem Pepa"
        };

        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    }

    [Fact]
    public void ErrorModel_DisplayNameInvalidCharacters()
    {
        // Arrange
        var model = new ErrorModel()
        {
            // DisplayName doesn't allow diacritics
            DisplayName = "Pepík_z_Brna",
            Content = "AHOJ ja jsem Pepa"
        };

        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    }

    [Fact]
    public void ErrorModel_ContentTooLong()
    {
        // Arrange
        var model = new ErrorModel()
        {
            DisplayName = "Pepa_z_Brna",
            Content = "a".PadRight(1403, 'a')
        };

        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    }

    [Fact]
    public void ErrorModel_ContentInvalidCharacters()
    {
        // Arrange
        var model = new ErrorModel()
        {
            // DisplayName doesn't allow diacritics
            DisplayName = "Pepík_z_Brna",
            Content = "AHOJ ja jsem Pepa 👋👋"
        };

        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    }
}