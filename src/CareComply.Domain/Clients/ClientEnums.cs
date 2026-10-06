namespace CareComply.Domain.Clients;

public enum ClientStatus
{
    Pending = 1,
    Active = 2,
    OnHold = 3,
    Discharged = 4,
    Inactive = 5,
}

public enum ClientGender
{
    Unknown = 0,
    Female = 1,
    Male = 2,
    NonBinary = 3,
    Other = 4,
    PreferNotToSay = 5,
}

public enum ClientProgramType
{
    None = 0,
    PcaTraditional = 1,
    PcaChoice = 2,
    Two45D = 3,
    CfssAgency = 4,
    CfssBudget = 5,
}

public enum ClientContactType
{
    General = 1,
    Emergency = 2,
    ResponsibleParty = 3,
    CareCoordinator = 4,
    ConsultationProvider = 5,
    Physician = 6,
    PreferredCaregiver = 7,
    Other = 8,
}
