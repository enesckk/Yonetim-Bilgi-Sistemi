namespace PersonelYonetim.Domain.Enums;

public enum OrganizationUnitStatus : byte
{
    Active = 1,
    Passive = 2,
    Closed = 3,
    UnderRenovation = 4,
    TemporarilyClosed = 5,
    Planning = 6,
    Transferred = 7,
    OutOfUse = 8
}
