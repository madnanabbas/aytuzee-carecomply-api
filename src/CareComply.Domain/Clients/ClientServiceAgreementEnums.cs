namespace CareComply.Domain.Clients;

public enum ClientServiceAgreementStatus
{
    Draft = 1,
    PendingAuthorization = 2,
    Active = 3,
    OnHold = 4,
    Expired = 5,
    Terminated = 6,
    Cancelled = 7,
}

public enum ClientServiceAgreementUnitType
{
    FifteenMinutes = 1,
    Hour = 2,
    Visit = 3,
    Day = 4,
    PerDiem = 5,
    ServiceUnit = 6,
    DollarAmount = 7,
}

public enum ClientServiceAgreementPriority
{
    Normal = 1,
    High = 2,
    Urgent = 3,
}
