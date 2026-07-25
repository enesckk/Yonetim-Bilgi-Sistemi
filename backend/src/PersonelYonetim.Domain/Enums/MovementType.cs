namespace PersonelYonetim.Domain.Enums;

public enum MovementType : byte
{
    UnitChange = 1,
    FacilityChange = 2,
    DutyChange = 3,
    TitleChange = 4,
    TemporaryAssignment = 5,
    PermanentAssignment = 6,
    AdditionalDutyAssigned = 7,
    DutyRemoved = 8,
    TransferToOtherDirectorate = 9,
    LeftJob = 10,
    Retirement = 11,
    ReturnToDuty = 12
}
