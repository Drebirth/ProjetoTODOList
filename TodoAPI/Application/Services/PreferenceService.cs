using Microsoft.EntityFrameworkCore;
using TodoAPI.Application.Contracts;
using TodoAPI.Domain.Entities;
using TodoAPI.Infrastructure.Persistence;

namespace TodoAPI.Application.Services;

public sealed record PreferenceUpdateResult(PreferenceResponse? Value, string? Error = null, bool Conflict = false, bool NotFound = false);

public sealed class PreferenceService(TodoDbContext db, SchedulingService scheduling)
{
    public async Task<PreferenceResponse?> GetAsync(int userId, CancellationToken ct) =>
        await db.Users.Where(x => x.Id == userId).Select(x => new PreferenceResponse(x.TimeZoneId)).SingleOrDefaultAsync(ct);

    public async Task<PreferenceUpdateResult> UpdateAsync(int userId, string timeZoneId, CancellationToken ct)
    {
        if (!SchedulingService.IsValidTimeZone(timeZoneId)) return new PreferenceUpdateResult(null, "TimeZoneId deve ser um identificador IANA válido.");
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return new PreferenceUpdateResult(null, NotFound: true);
        var windows = await scheduling.GetWindowsAsync(userId, timeZoneId, ct: ct);
        if (SchedulingService.ValidateNoConflicts(windows) is not null)
            return new PreferenceUpdateResult(null, Conflict: true);
        user.TimeZoneId = timeZoneId;
        await db.SaveChangesAsync(ct);
        return new PreferenceUpdateResult(new PreferenceResponse(user.TimeZoneId));
    }
}
