namespace PersonelYonetim.Domain.Events;

/// <summary>Salon / mahalle etkinliğinde gösterilecek katılım sayısı.</summary>
public static class EventAttendance
{
    public static int? Resolve(int? actual, int settlementSum, int? expected)
    {
        if (actual.HasValue) return actual;
        if (settlementSum > 0) return settlementSum;
        return expected;
    }
}
