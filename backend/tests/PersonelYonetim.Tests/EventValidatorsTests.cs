using FluentValidation.TestHelper;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Tests;

public class EventValidatorsTests
{
    private static readonly DateTime PastDate = new(2024, 2, 10, 7, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(EventStatus.Published)]
    [InlineData(EventStatus.Completed)]
    public void Create_accepts_past_event_with_settlement_only(EventStatus status)
    {
        var command = new CreateEventCommand
        {
            Title = "Geçmiş etkinlik",
            StartAtUtc = PastDate,
            Status = status,
            Settlements = [new EventSettlementInputDto { SettlementId = Guid.NewGuid() }]
        };

        new CreateEventCommandValidator().TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Update_accepts_completed_event_with_settlement_only()
    {
        var command = new UpdateEventCommand
        {
            Id = Guid.NewGuid(),
            Title = "Geçmiş etkinlik",
            StartAtUtc = PastDate,
            Status = EventStatus.Completed,
            Settlements = [new EventSettlementInputDto { SettlementId = Guid.NewGuid() }]
        };

        new UpdateEventCommandValidator().TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_still_requires_a_location_without_settlement_or_facility()
    {
        var command = new CreateEventCommand
        {
            Title = "Konumsuz etkinlik",
            StartAtUtc = PastDate,
            Status = EventStatus.Completed
        };

        var result = new CreateEventCommandValidator().TestValidate(command);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("mahalle"));
    }
}
