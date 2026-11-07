using TodoAPI.Domain.Entities;

namespace TodoAPI.Domain.Services;

public static class RecurrenceSchedule
{
    public static DateTime CalculateNextDueUtc(DateTime previousDueUtc, RecurrenceFrequency frequency)
    {
        var utc = DateTime.SpecifyKind(previousDueUtc, DateTimeKind.Utc);
        return frequency switch
        {
            RecurrenceFrequency.Diaria => utc.AddDays(1),
            RecurrenceFrequency.Semanal => utc.AddDays(7),
            RecurrenceFrequency.Mensal => utc.AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Frequência inválida.")
        };
    }
}
