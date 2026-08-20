using System.Diagnostics.CodeAnalysis;

namespace CareComply.Application.Authorization;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "IRequirePermission is the approved ubiquitous authorization contract name.")]
public interface IRequirePermission
{
    string RequiredPermission { get; }
}
