using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.UpdateUserGeneral;

public sealed class UpdateUserGeneralCommandHandler(IUserGeneralAdministration administration)
    : ICommandHandler<UpdateUserGeneralCommand, UpdateUserGeneralResult>
{
    public async ValueTask<UpdateUserGeneralResult> Handle(
        UpdateUserGeneralCommand command,
        CancellationToken cancellationToken) =>
        UpdateUserGeneralResult.Succeeded(await administration.UpdateAsync(
            new(
                command.UserId,
                command.CompanyId,
                command.FirstName.Trim(),
                command.LastName.Trim(),
                command.Email.Trim(),
                string.IsNullOrWhiteSpace(command.PhoneNumber) ? null : command.PhoneNumber.Trim(),
                string.IsNullOrWhiteSpace(command.Password) ? null : command.Password,
                command.EditorVersion,
                command.LocationIds ?? []),
            cancellationToken));
}
