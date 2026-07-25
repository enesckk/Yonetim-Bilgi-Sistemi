using PersonelYonetim.Domain.Common;

namespace PersonelYonetim.Domain.Entities;

public class RolePermission : AuditableEntity
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
