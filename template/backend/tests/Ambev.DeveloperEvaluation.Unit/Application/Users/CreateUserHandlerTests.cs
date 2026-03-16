using Ambev.DeveloperEvaluation.Application.Users.CreateUser;
using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Users;

public class CreateUserHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly AutoMapper.IMapper _mapper;
    private readonly IPasswordHasher _passwordHasher;
    private readonly CreateUserHandler _handler;

    public CreateUserHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _mapper = Substitute.For<AutoMapper.IMapper>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _handler = new CreateUserHandler(_userRepository, _mapper, _passwordHasher);
    }

    [Fact(DisplayName = "Given valid command When Handle Then creates user and returns result")]
    public async Task Handle_ValidCommand_ReturnsResult()
    {
        var command = new CreateUserCommand
        {
            Username = "Marco Silva",
            Email = "marco@email.com",
            Password = "Aa1!aaaa",
            Phone = "+5511999999999",
            Role = UserRole.Admin,
            Status = UserStatus.Active
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = command.Username,
            Email = command.Email,
            Password = "plain",
            Phone = command.Phone,
            Role = command.Role,
            Status = command.Status
        };

        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _mapper.Map<User>(command).Returns(user);
        _passwordHasher.HashPassword(command.Password).Returns("hashed-pass");
        _userRepository.CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<User>(0));

        var expected = new CreateUserResult
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            Status = user.Status,
            CreatedAt = user.CreatedAt
        };
        _mapper.Map<CreateUserResult>(Arg.Any<User>()).Returns(expected);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Email.Should().Be(command.Email);
        result.Username.Should().Be(command.Username);
        result.Role.Should().Be(UserRole.Admin);
        await _userRepository.Received(1).CreateAsync(Arg.Is<User>(u => u.Password == "hashed-pass"), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given existing email When Handle Then throws InvalidOperationException")]
    public async Task Handle_ExistingEmail_ThrowsInvalidOperationException()
    {
        var command = new CreateUserCommand
        {
            Username = "Marco Silva",
            Email = "marco@email.com",
            Password = "Aa1!aaaa",
            Phone = "+5511999999999",
            Role = UserRole.Admin,
            Status = UserStatus.Active
        };

        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(new User { Id = Guid.NewGuid(), Email = command.Email });

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"User with email {command.Email} already exists");
    }

    [Fact(DisplayName = "Given invalid command When Handle Then throws ValidationException")]
    public async Task Handle_InvalidCommand_ThrowsValidationException()
    {
        var command = new CreateUserCommand
        {
            Username = "",
            Email = "invalid-email",
            Password = "123",
            Phone = "invalid",
            Role = UserRole.None,
            Status = UserStatus.Unknown
        };

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        await _userRepository.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}

