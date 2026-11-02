namespace PersonelYonetim.Domain.Authorization;

/// <summary>Director event access remains read-only, including with legacy permission claims.</summary>
public static class EventAccessPolicy
{
    public static bool Allows(string permission, IEnumerable<string> roles)
    {
        if (permission != PermissionCodes.EventsManage) return true;
        var codes = roles.ToHashSet();
        return !codes.Contains(RoleCodes.Director) || codes.Contains(RoleCodes.SystemAdmin);
    }
}
