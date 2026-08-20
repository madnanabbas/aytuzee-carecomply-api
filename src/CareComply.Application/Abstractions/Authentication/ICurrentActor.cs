namespace CareComply.Application.Abstractions.Authentication;

public interface ICurrentActor
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }
}
