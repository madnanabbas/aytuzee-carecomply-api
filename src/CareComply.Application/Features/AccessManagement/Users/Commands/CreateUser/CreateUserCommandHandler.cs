using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.CreateUser;

public sealed class CreateUserCommandHandler(IUserProvisioning provisioning)
    : ICommandHandler<CreateUserCommand, CreateUserResult>
{
    public async ValueTask<CreateUserResult> Handle(
        CreateUserCommand command,
        CancellationToken cancellationToken) =>
        CreateUserResult.Completed(await provisioning.CreateAsync(
            new(
                command.FirstName.Trim(),
                command.LastName.Trim(),
                command.Email.Trim(),
                command.Password,
                string.IsNullOrWhiteSpace(command.PhoneNumber) ? null : command.PhoneNumber.Trim(),
                command.IsActive,
                command.RequiresMfa,
                command.MfaMethod,
                command.RoleIds),
            cancellationToken));
}
