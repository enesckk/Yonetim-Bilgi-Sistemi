namespace PersonelYonetim.Application.Common.Interfaces;

/// <summary>Hassas alan / dosya görüntüleme denetim kaydı (KVKK izlenebilirlik).</summary>
public interface ISensitiveAccessLogger
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        object? details = null,
        CancellationToken cancellationToken = default);
}

public static class SensitiveAccessActions
{
    public const string ViewNationalId = "ViewNationalId";
    public const string ViewSpecialConditions = "ViewSpecialConditions";
    public const string ViewSpecialConditionDocument = "ViewSpecialConditionDocument";
    public const string ViewEmployeePhoto = "ViewEmployeePhoto";
    public const string ViewAddress = "ViewAddress";
    public const string ViewPhone = "ViewPhone";
}
