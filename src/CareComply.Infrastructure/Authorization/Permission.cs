using System.Diagnostics.CodeAnalysis;

namespace CareComply.Infrastructure.Authorization;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Permission is the approved CareComply security persistence term.")]
public sealed class Permission
{
    private Permission()
    {
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;
}
