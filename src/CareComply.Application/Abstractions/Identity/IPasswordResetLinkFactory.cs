namespace CareComply.Application.Abstractions.Identity;

public interface IPasswordResetLinkFactory
{
    string? Create(Guid userId, string urlSafeToken);
}
