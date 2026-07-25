using PersonelYonetim.Domain.Common;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Domain.Entities;

/// <summary>Yetkinlik kataloğu (Robotik kodlama, Excel vb.).</summary>
public class Skill : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public SkillCategory Category { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<EmployeeSkill> EmployeeSkills { get; set; } = new List<EmployeeSkill>();
}
