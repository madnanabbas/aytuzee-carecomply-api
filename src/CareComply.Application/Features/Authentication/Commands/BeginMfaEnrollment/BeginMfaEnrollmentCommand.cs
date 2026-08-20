using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.BeginMfaEnrollment;

public sealed record BeginMfaEnrollmentCommand : ICommand<MfaEnrollmentData?>;
