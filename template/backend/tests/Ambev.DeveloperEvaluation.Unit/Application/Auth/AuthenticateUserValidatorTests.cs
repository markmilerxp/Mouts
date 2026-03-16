using Ambev.DeveloperEvaluation.Application.Auth.AuthenticateUser;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Auth;

public class AuthenticateUserValidatorTests
{
    [Fact(DisplayName = "Given valid command When Validate Then command is valid")]
    public void Validate_ValidCommand_IsValid()
    {
        var validator = new AuthenticateUserValidator();
        var command = new AuthenticateUserCommand
        {
            Email = "user@example.com",
            Password = "123456"
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Given invalid auth command When Validate Then command is invalid")]
    [InlineData("", "123456")]
    [InlineData("invalid-email", "123456")]
    [InlineData("user@example.com", "123")]
    public void Validate_InvalidCommand_IsInvalid(string email, string password)
    {
        var validator = new AuthenticateUserValidator();
        var command = new AuthenticateUserCommand
        {
            Email = email,
            Password = password
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}

