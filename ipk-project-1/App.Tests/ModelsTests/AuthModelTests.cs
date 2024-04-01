using System.ComponentModel.DataAnnotations;
using App.Models;
using App.Transport;
using Xunit;
using NSubstitute;

namespace App.Tests;

public class AuthModelTests
{
    [Fact]
    public void AuthModel_Valid()
    {
        // Arrange
        var model = new AuthModel()
        {
            DisplayName = "Pepa_z_Brna",
            Secret = "1234-5678-abdc",
            Username = "pepa"
        };

        // Act & Assert
        var exception = Record.Exception(() => ModelValidator.Validate(model));
        Assert.Null(exception);
    }

    [Fact]
    public void AuthModel_DisplayNameTooLong()
    {
        // Arrange
        var model = new AuthModel()
        {
            DisplayName = "a".PadRight(129, 'a'),
            Secret = "1234-5678-abdc",
            Username = "pepa"
        };

        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    }

    [Fact]
    public void AuthModel_DisplayNameInvalidCharacters()
    {
        // Arrange
        var model = new AuthModel()
        {
            // DisplayName doesn't allow diacritics
            DisplayName = "Pepík_z_Brna",
            Secret = "1234-5678-abdc",
            Username = "pepa"
        };

        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    }

    [Fact]
    public void AuthModel_UserNameTooLong()
    {
        // Arrange
        var model = new AuthModel()
        {
            Username = "a".PadRight(129, 'a'),
            Secret = "1234-5678-abdc",
            DisplayName = "Pepa_z_Brna"
        };

        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    }

    [Fact]
    public void AuthModel_UsernameInvalidCharacters()
    {
        // Arrange
        var model = new AuthModel()
        {
            Username = "😵‍💫😵‍💫😵‍💫😵‍💫",
            Secret = "1234-5678-abdc",
            DisplayName = "Pepa_z_Brna"
        };

        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    }

    [Fact]
    public void AuthModel_SecretTooLong()
    {
        // Arrange
        var model = new AuthModel()
        {
            Username = "pepa",
            Secret = "a".PadRight(129, 'a'),
            DisplayName = "Pepa_z_Brna"
        };

        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    }

    [Fact]
    public void AuthModel_SecretInvalidCharacters()
    {
        // Arrange
        var model = new AuthModel()
        {
            Username = "😵‍💫😵‍💫😵‍💫😵‍💫",
            Secret = "1234-5678-abdc-čččččč",
            DisplayName = "Pepa_z_Brna"
        };

        // Act & Assert
        Assert.Throws<ValidationException>(() => ModelValidator.Validate(model));
    }

    [Fact]
    public void AuthModel_CommandValid()
    {
        // Arrange
        var data = "pepa 1234-5678-abdc Pepa_z_Brna";

        // Act
        var model = AuthModel.Parse(data);

        // Assert
        Assert.Equal("pepa", model.Username);
        Assert.Equal("1234-5678-abdc", model.Secret);
        Assert.Equal("Pepa_z_Brna", model.DisplayName);
    }

    [Fact]
    public void AuthModel_CommandInvalid()
    {
        // Arrange
        var data = "pepa 1234-5678-abdc Pepa_z_Brna HEHE";

        // Act & Assert
        Assert.Throws<ValidationException>(() => AuthModel.Parse(data));
    }
}