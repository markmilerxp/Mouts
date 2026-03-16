using Ambev.DeveloperEvaluation.Application.Users.GetUser;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Users;

public class GetUserHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly AutoMapper.IMapper _mapper;
    private readonly GetUserHandler _handler;

    public GetUserHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _mapper = Substitute.For<AutoMapper.IMapper>();
        _handler = new GetUserHandler(_userRepository, _mapper);
    }

    [Fact(DisplayName = "Given existing user id When Handle Then returns mapped user")]
    public async Task Handle_ExistingUser_ReturnsResult()
    {
        var userId = Guid.NewGuid();
        var command = new GetUserCommand(userId);
        var user = new User
        {
            Id = userId,
            Username = "Marco Silva",
            Email = "marco@email.com",
            Phone = "+5511999999999",
            Role = UserRole.Manager,
            Status = UserStatus.Active
        };

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var expected = new GetUserResult
        {
            Id = userId,
            Name = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            Status = user.Status
        };
        _mapper.Map<GetUserResult>(user).Returns(expected);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.Name.Should().Be("Marco Silva");
        result.Role.Should().Be(UserRole.Manager);
    }

    [Fact(DisplayName = "Given non-existing user id When Handle Then throws KeyNotFoundException")]
    public async Task Handle_UserNotFound_ThrowsKeyNotFoundException()
    {
        var command = new GetUserCommand(Guid.NewGuid());
        _userRepository.GetByIdAsync(command.Id, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"User with ID {command.Id} not found");
    }

    [Fact(DisplayName = "Given empty id When Handle Then throws ValidationException")]
    public async Task Handle_EmptyId_ThrowsValidationException()
    {
        var command = new GetUserCommand(Guid.Empty);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        await _userRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}

