using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.ResendMfaEmailCode;

public sealed record ResendMfaEmailCodeCommand : ICommand<MfaEmailSendStatus>;
