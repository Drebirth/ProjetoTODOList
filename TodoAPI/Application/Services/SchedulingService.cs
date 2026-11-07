using Microsoft.EntityFrameworkCore;
using TodoAPI.Domain.Entities;
using TodoAPI.Infrastructure.Persistence;

namespace TodoAPI.Application.Services;

public sealed record ScheduledWindow(DateTime StartUtc, DateTime EndUtc, int? TodoId = null, int? SeriesId = null, int? OccurrenceNumber = null);

public sealed class SchedulingService(TodoDbContext db)
{
    public static bool IsValidTimeZone(string id)
    {
        if (id != "UTC" && !id.Contains('/')) return false;
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    public static string? ValidateWindow(DateTimeOffset? start, DateTimeOffset? end, out DateTime? startUtc, out DateTime? endUtc)
    {
        startUtc = start?.UtcDateTime;
        endUtc = end?.UtcDateTime;
        if (start is null != (end is null)) return "DataInicioUtc e DataFimUtc devem ser informadas juntas.";
        if (startUtc is not null && endUtc <= startUtc) return "DataFimUtc deve ser posterior a DataInicioUtc.";
        return null;
    }

    public static bool IsSameLocalDate(DateTime startUtc, DateTime endUtc, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), zone))
            == DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc), zone));
    }

    public static (DateOnly Date, TimeOnly Start, TimeOnly End) ToLocalAnchor(DateTime startUtc, DateTime endUtc, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var start = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), zone);
        var end = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc), zone);
        return (DateOnly.FromDateTime(start), TimeOnly.FromDateTime(start), TimeOnly.FromDateTime(end));
    }

    public static ScheduledWindow Project(RecurrenceSeries series, string timeZoneId, int number)
    {
        var step = number - (series.ScheduleAnchorOccurrenceNumber ?? 1);
        var anchor = series.ScheduleAnchorLocalDate!.Value;
        var date = series.Frequency switch
        {
            RecurrenceFrequency.Diaria => anchor.AddDays(step),
            RecurrenceFrequency.Semanal => anchor.AddDays(step * 7),
            RecurrenceFrequency.Mensal => anchor.AddMonths(step),
            _ => throw new InvalidOperationException("Frequência inválida para projeção.")
        };
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var start = ResolveLocal(date.ToDateTime(series.ScheduleStartLocalTime!.Value), zone);
        var end = ResolveLocal(date.ToDateTime(series.ScheduleEndLocalTime!.Value), zone);
        return new ScheduledWindow(start, end, SeriesId: series.Id, OccurrenceNumber: number);
    }

    public static DateTime ResolveLocal(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            var before = local;
            while (zone.IsInvalidTime(before)) before = before.AddMinutes(-1);
            var after = local;
            while (zone.IsInvalidTime(after)) after = after.AddMinutes(1);
            var gap = zone.GetUtcOffset(after) - zone.GetUtcOffset(before);
            local = local.Add(gap);
        }

        if (zone.IsAmbiguousTime(local))
        {
            var offset = zone.GetAmbiguousTimeOffsets(local).Max(); // Larger offset gives the first UTC occurrence.
            return new DateTimeOffset(local, offset).UtcDateTime;
        }
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    public async Task<List<ScheduledWindow>> GetWindowsAsync(int userId, string timeZoneId, int? excludeTodoId = null, CancellationToken ct = default)
    {
        var todos = await db.Todos.AsNoTracking().Where(x => x.UserId == userId && x.DataInicioUtc != null && x.DataFimUtc != null
            && (excludeTodoId == null || x.Id != excludeTodoId))
            .Select(x => new { x.Id, Start = x.DataInicioUtc!.Value, End = x.DataFimUtc!.Value, x.SerieRecorrenciaId, x.NumeroOcorrencia })
            .ToListAsync(ct);
        var result = todos.Select(x => new ScheduledWindow(x.Start, x.End, x.Id, x.SerieRecorrenciaId, x.NumeroOcorrencia)).ToList();

        var series = await db.RecurrenceSeries.AsNoTracking().Where(x => x.UserId == userId && x.IsActive
            && x.Frequency != null && x.Duration != null && x.ScheduleAnchorLocalDate != null
            && x.ScheduleStartLocalTime != null && x.ScheduleEndLocalTime != null).ToListAsync(ct);
        var existing = todos.Where(x => x.SerieRecorrenciaId != null && x.NumeroOcorrencia != null)
            .Select(x => (x.SerieRecorrenciaId!.Value, x.NumeroOcorrencia!.Value)).ToHashSet();
        foreach (var recurrence in series)
        {
            for (var number = recurrence.NextOccurrenceNumber; number <= recurrence.Duration; number++)
            {
                if (!existing.Contains((recurrence.Id, number))) result.Add(Project(recurrence, timeZoneId, number));
            }
        }
        return result;
    }

    public async Task<ScheduledWindow?> FindConflictAsync(int userId, string timeZoneId, DateTime startUtc, DateTime endUtc,
        int? excludeTodoId = null, CancellationToken ct = default)
    {
        var candidate = new ScheduledWindow(startUtc, endUtc);
        return (await GetWindowsAsync(userId, timeZoneId, excludeTodoId, ct))
            .FirstOrDefault(x => Overlaps(candidate, x));
    }

    public async Task<ScheduledWindow?> FindConflictAsync(int userId, string timeZoneId, IReadOnlyCollection<ScheduledWindow> candidates,
        int? excludeTodoId = null, CancellationToken ct = default)
    {
        var occupied = await GetWindowsAsync(userId, timeZoneId, excludeTodoId, ct);
        var candidateKeys = candidates.Where(x => x.SeriesId is not null && x.OccurrenceNumber is not null)
            .Select(x => (x.SeriesId!.Value, x.OccurrenceNumber!.Value)).ToHashSet();
        foreach (var candidate in candidates)
        {
            var conflict = occupied.FirstOrDefault(x =>
                !(x.SeriesId is not null && x.OccurrenceNumber is not null
                    && candidateKeys.Contains((x.SeriesId.Value, x.OccurrenceNumber.Value)))
                && Overlaps(candidate, x));
            if (conflict is not null) return conflict;
        }
        for (var i = 0; i < candidates.Count; i++)
            for (var j = i + 1; j < candidates.Count; j++)
                if (Overlaps(candidates.ElementAt(i), candidates.ElementAt(j))) return candidates.ElementAt(j);
        return null;
    }

    public static bool Overlaps(ScheduledWindow a, ScheduledWindow b) => a.StartUtc < b.EndUtc && b.StartUtc < a.EndUtc;

    public static string? ValidateNoConflicts(IEnumerable<ScheduledWindow> windows)
    {
        var items = windows.ToArray();
        for (var i = 0; i < items.Length; i++)
            for (var j = i + 1; j < items.Length; j++)
                if (Overlaps(items[i], items[j])) return "A agenda contém janelas sobrepostas.";
        return null;
    }
}
