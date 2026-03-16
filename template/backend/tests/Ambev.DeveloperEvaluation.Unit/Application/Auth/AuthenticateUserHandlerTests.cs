using Ambev.DeveloperEvaluation.Application.Auth.AuthenticateUser;
using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Specifications;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Auth;

public class AuthenticateUserHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly AuthenticateUserHandler _handler;

    public AuthenticateUserHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
        _handler = new AuthenticateUserHandler(_userRepository, _passwordHasher, _jwtTokenGenerator);
    }

    [Fact(DisplayName = "Given valid credentials and active user When Handle Then returns token and user data")]
    public async Task Handle_ValidCredentials_ReturnsToken()
    {
        var command = new AuthenticateUserCommand
        {
            Email = "user@example.com",
            Password = "P@ssw0rd"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            Username = "Test User",
            Password = "hashed",
            Role = UserRole.Admin,
            Status = UserStatus.Active
        };

        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyPassword(command.Password, user.Password)
            .Returns(true);
        _jwtTokenGenerator.GenerateToken(user)
            .Returns("fake-jwt-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Token.Should().Be("fake-jwt-token");
        result.Email.Should().Be(command.Email);
        result.Name.Should().Be(user.Username);
        result.Role.Should().Be(UserRole.Admin.ToString());
    }

    [Fact(DisplayName = "Given invalid password When Handle Then throws UnauthorizedAccessException")]
    public async Task Handle_InvalidPassword_ThrowsUnauthorized()
    {
        var command = new AuthenticateUserCommand
        {
            Email = "user@example.com",
            Password = "wrong"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            Username = "Test User",
            Password = "hashed",
            Role = UserRole.Admin,
            Status = UserStatus.Active
        };

        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyPassword(command.Password, user.Password)
            .Returns(false);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
    }

    [Fact(DisplayName = "Given inactive user When Handle Then throws UnauthorizedAccessException")]
    public async Task Handle_InactiveUser_ThrowsUnauthorized()
    {
        var command = new AuthenticateUserCommand
        {
            Email = "user@example.com",
            Password = "P@ssw0rd"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            Username = "Inactive User",
            Password = "hashed",
            Role = UserRole.Admin,
            Status = UserStatus.Inactive
        };

        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyPassword(command.Password, user.Password)
            .Returns(true);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User is not active");
    }
}

