using FluentValidation.TestHelper;
using PersonelYonetim.Application.Features.Map;

namespace PersonelYonetim.Tests;

public class SettlementPopulationValidatorTests
{
    private readonly UpsertSettlementPopulationValidator _validator = new();

    [Fact]
    public void Accepts_total_without_breakdown()
    {
        var result = _validator.TestValidate(new UpsertSettlementPopulationCommand
        {
            SettlementId = Guid.NewGuid(),
            Year = 2025,
            Population = 1200,
            Source = "manual"
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rejects_breakdown_that_does_not_sum_to_population()
    {
        var result = _validator.TestValidate(new UpsertSettlementPopulationCommand
        {
            SettlementId = Guid.NewGuid(),
            Year = 2025,
            Population = 100,
            MaleCount = 40,
            FemaleCount = 40,
            ChildCount = 10,
            Source = "manual"
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Accepts_matching_breakdown()
    {
        var result = _validator.TestValidate(new UpsertSettlementPopulationCommand
        {
            SettlementId = Guid.NewGuid(),
            Year = 2025,
            Population = 100,
            MaleCount = 40,
            FemaleCount = 40,
            ChildCount = 20,
            Source = "manual"
        });
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class SettlementSchoolValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(50001)]
    public void Rejects_missing_or_out_of_range_school_population(int? count)
    {
        var validator = new UpsertSettlementSchoolValidator();
        validator.TestValidate(new UpsertSettlementSchoolCommand
        {
            SettlementId = Guid.NewGuid(), Name = "Örnek İlkokulu", StudentCount = count
        }).ShouldHaveValidationErrorFor(x => x.StudentCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(450)]
    [InlineData(50000)]
    public void Accepts_explicit_school_population(int count)
    {
        var validator = new UpsertSettlementSchoolValidator();
        validator.TestValidate(new UpsertSettlementSchoolCommand
        {
            SettlementId = Guid.NewGuid(), Name = "Örnek İlkokulu", StudentCount = count
        }).ShouldNotHaveAnyValidationErrors();
    }
}
