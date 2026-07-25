namespace PersonelYonetim.Domain.Enums;

/// <summary>
/// Görev kategorisi (fiili iş türü gruplaması).
/// </summary>
public enum DutyCategory : byte
{
    Manager = 1,
    Administrative = 2,
    Instructor = 3,
    Technical = 4,
    Reception = 5,
    Library = 6,
    Auxiliary = 7,
    Cleaning = 8,
    Project = 9,
    SocialMedia = 10,
    PublicRelations = 11,
    Other = 99
}
