namespace TodoAPI.Application.Contracts;

public sealed record ServiceResult<T>(T? Value, string? ValidationError = null, bool NotFound = false, bool Conflict = false);
