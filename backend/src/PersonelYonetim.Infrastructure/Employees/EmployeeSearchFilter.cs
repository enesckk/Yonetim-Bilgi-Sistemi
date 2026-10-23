using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Employees;

internal static class EmployeeSearchFilter
{
    public static IQueryable<Employee> Apply(IQueryable<Employee> employees, EmployeeListQuery query, ICurrentUserService currentUser)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant().Replace("İ", "i").Replace("ı", "i");
            if (query.NameSearch && !query.Search.Any(char.IsDigit))
            {
                var nameTerm = string.Join(" ", term.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                var wordTerm = " " + nameTerm;
                employees = employees.Where(x =>
                    (x.FirstName + " " + x.LastName).ToLower().Replace("İ", "i").Replace("ı", "i").StartsWith(nameTerm) ||
                    (x.FirstName + " " + x.LastName).ToLower().Replace("İ", "i").Replace("ı", "i").Contains(wordTerm));
            }
            else
            {
                var canSearchPhone = currentUser.HasPermission(PermissionCodes.EmployeesViewPhone);
                var phoneTerm = query.Search.Trim().All(c => char.IsDigit(c) || "+()-. ".Contains(c))
                    ? new string(query.Search.Where(char.IsDigit).ToArray()) : "";
                if (phoneTerm.Length == 14 && phoneTerm.StartsWith("0090")) phoneTerm = phoneTerm[4..];
                else if (phoneTerm.Length == 12 && phoneTerm.StartsWith("90")) phoneTerm = phoneTerm[2..];
                else if (phoneTerm.Length == 11 && phoneTerm.StartsWith('0')) phoneTerm = phoneTerm[1..];
                employees = employees.Where(x =>
                    x.FirstName.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term) ||
                    x.LastName.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term) ||
                    (x.FirstName + " " + x.LastName).ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term) ||
                    (x.EmployeeNumber != null && x.EmployeeNumber.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term)) ||
                    (canSearchPhone && x.PersonalPhone != null && (x.PersonalPhone.Contains(term) || (phoneTerm != "" && x.PersonalPhone.Replace(" ", "").Replace("(", "").Replace(")", "").Replace("-", "").Replace(".", "").Replace("+", "").Contains(phoneTerm)))) ||
                    (canSearchPhone && x.CorporatePhone != null && (x.CorporatePhone.Contains(term) || (phoneTerm != "" && x.CorporatePhone.Replace(" ", "").Replace("(", "").Replace(")", "").Replace("-", "").Replace(".", "").Replace("+", "").Contains(phoneTerm)))) ||
                    (x.PersonalEmail != null && x.PersonalEmail.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term)) ||
                    (x.CorporateEmail != null && x.CorporateEmail.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term)) ||
                    (x.JobTitle != null && x.JobTitle.Name.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term)) ||
                    (x.Unit != null && x.Unit.Name.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term)) ||
                    (x.Facility != null && x.Facility.Name.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term)) ||
                    (x.EmploymentType != null && x.EmploymentType.Name.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term)) ||
                    x.Assignments.Any(a => a.JobDuty != null && a.JobDuty.Name.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term)) ||
                    x.EducationRecords.Any(e =>
                        (e.Department != null && e.Department.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term)) ||
                        (e.University != null && e.University.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term)) ||
                        (e.School != null && e.School.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term))) ||
                    x.Skills.Any(s => s.Skill != null && s.Skill.Name.ToLower().Replace("İ", "i").Replace("ı", "i").Contains(term)));
            }
        }
        return employees;
    }
}
