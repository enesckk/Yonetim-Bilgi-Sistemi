namespace PersonelYonetim.Domain.Enums;

/// <summary>
/// Not görünürlük seviyesi — herkes her notu göremez.
/// </summary>
public enum NoteVisibility : byte
{
    AuthorOnly = 1,
    UnitManagers = 2,
    DirectorateManagers = 3,
    PrivilegedUsers = 4,
    SystemAdministrators = 5
}
