using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal static class ClientPersistenceErrorClassifier
{
    public static bool IsDuplicatePmi(DbUpdateException exception) =>
        IsConstraint(exception, "UX_Clients_PmiNumber");

    public static bool IsServiceHistoryConflict(DbUpdateException exception) =>
        IsConstraint(exception, "UX_ClientServices_Client_Code_StartDate")
        || IsConstraint(exception, "UX_ClientServices_Client_Code_Active");

    private static bool IsConstraint(DbUpdateException exception, string constraintName) =>
        exception.GetBaseException().Message.Contains(
            constraintName,
            StringComparison.OrdinalIgnoreCase);
}
