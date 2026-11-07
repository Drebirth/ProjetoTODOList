using Microsoft.EntityFrameworkCore;
using TodoAPI.Application.Contracts;
using TodoAPI.Domain.Entities;
using TodoAPI.Infrastructure.Persistence;

namespace TodoAPI.Application.Services;

public sealed class TodoService(TodoDbContext dbContext, RecurrenceService recurrenceService, SchedulingService schedulingService)
{
    public async Task<ServiceResult<TodoResponse>> CreateAsync(
        int userId,
        string titulo,
        string descricao,
        bool concluido,
        int? categoriaId,
        Prioridade? prioridade,
        RecurrenceFrequency? frequenciaRecorrencia,
        int? duracaoRecorrencia,
        DateTimeOffset? dataInicioUtc,
        DateTimeOffset? dataFimUtc,
        CancellationToken cancellationToken)
    {
        var validationError = RecurrenceService.ValidateConfiguration(frequenciaRecorrencia, duracaoRecorrencia);
        if (validationError is not null)
        {
            return new ServiceResult<TodoResponse>(null, validationError);
        }

        var category = await GetOwnedCategoryAsync(userId, categoriaId, cancellationToken);
        if (categoriaId is not null && category is null)
            return new ServiceResult<TodoResponse>(null, "Categoria inválida para o usuário autenticado.");
        if (prioridade is not null && !Enum.IsDefined(prioridade.Value))
            return new ServiceResult<TodoResponse>(null, "Prioridade inválida.");

        var windowError = SchedulingService.ValidateWindow(dataInicioUtc, dataFimUtc, out var startUtc, out var endUtc);
        if (windowError is not null) return new ServiceResult<TodoResponse>(null, windowError);
        var timezoneId = await dbContext.Users.Where(x => x.Id == userId).Select(x => x.TimeZoneId).SingleAsync(cancellationToken);
        if (startUtc is not null && !SchedulingService.IsSameLocalDate(startUtc.Value, endUtc!.Value, timezoneId))
            return new ServiceResult<TodoResponse>(null, "A janela deve começar e terminar na mesma data local.");
        var proposedSeries = BuildProposedSeries(null, userId, titulo.Trim(), descricao.Trim(), frequenciaRecorrencia,
            duracaoRecorrencia, startUtc, endUtc, timezoneId);
        var proposedWindows = BuildCandidateWindows(startUtc, endUtc, proposedSeries, timezoneId);
        if (proposedWindows.Count > 0 && await schedulingService.FindConflictAsync(userId, timezoneId, proposedWindows, ct: cancellationToken) is not null)
            return new ServiceResult<TodoResponse>(null, "A janela conflita com outro horário agendado.", Conflict: true);

        var nowUtc = DateTime.UtcNow;
        var todo = new TodoItem
        {
            UserId = userId,
            Titulo = titulo.Trim(),
            Descricao = descricao.Trim(),
            CategoriaId = categoriaId,
            Categoria = category,
            Prioridade = prioridade ?? Prioridade.Media,
            DataInicioUtc = startUtc,
            DataFimUtc = endUtc,
            DataCriacao = nowUtc,
            DataConclusaoUtc = concluido ? nowUtc : null,
            Concluido = concluido
        };

        if (frequenciaRecorrencia is not null && duracaoRecorrencia is not null)
        {
            recurrenceService.AttachNewSeries(todo, userId, frequenciaRecorrencia.Value, duracaoRecorrencia.Value, nowUtc);
            if (proposedSeries is not null)
            {
                todo.SerieRecorrencia!.ScheduleAnchorLocalDate = proposedSeries.ScheduleAnchorLocalDate;
                todo.SerieRecorrencia.ScheduleAnchorOccurrenceNumber = proposedSeries.ScheduleAnchorOccurrenceNumber;
                todo.SerieRecorrencia.ScheduleStartLocalTime = proposedSeries.ScheduleStartLocalTime;
                todo.SerieRecorrencia.ScheduleEndLocalTime = proposedSeries.ScheduleEndLocalTime;
            }
            if (concluido)
            {
                recurrenceService.CreateNextOccurrence(todo.SerieRecorrencia!, nowUtc, timezoneId);
            }
        }

        dbContext.Todos.Add(todo);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ServiceResult<TodoResponse>(await ToResponseAsync(todo, cancellationToken));
    }

    public async Task<TodoPageResponse> GetPageAsync(int userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var userTodos = dbContext.Todos.AsNoTracking().Where(todo => todo.UserId == userId);
        var totalCount = await userTodos.CountAsync(cancellationToken);
        var items = await userTodos
            .OrderBy(todo => todo.DataCriacao)
            .ThenBy(todo => todo.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(todo => new TodoResponse(
                todo.Id,
                todo.Titulo,
                todo.Descricao,
                todo.CategoriaId,
                todo.Categoria == null ? null : todo.Categoria.Nome,
                todo.Prioridade,
                todo.DataCriacao,
                todo.Concluido,
                todo.DataConclusaoUtc,
                todo.DataPrevistaUtc,
                todo.DataInicioUtc,
                todo.DataFimUtc,
                todo.SerieRecorrencia == null ? null : todo.SerieRecorrencia.Frequency,
                todo.SerieRecorrencia == null ? null : todo.SerieRecorrencia.Duration,
                todo.SerieRecorrenciaId,
                todo.NumeroOcorrencia))
            .ToListAsync(cancellationToken);

        return new TodoPageResponse(items, page, pageSize, totalCount);
    }

    public async Task<TodoResponse?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var todo = await FindOwnedTodo(userId, id)
            .Include(item => item.SerieRecorrencia)
            .Include(item => item.Categoria)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return todo is null ? null : ToResponse(todo);
    }

    public async Task<ServiceResult<TodoResponse>> UpdateAsync(
        int userId,
        int id,
        string titulo,
        string descricao,
        bool concluido,
        int? categoriaId,
        Prioridade? prioridade,
        RecurrenceFrequency? frequenciaRecorrencia,
        int? duracaoRecorrencia,
        DateTimeOffset? dataInicioUtc,
        DateTimeOffset? dataFimUtc,
        CancellationToken cancellationToken)
    {
        var todo = await FindOwnedTodo(userId, id)
            .Include(item => item.SerieRecorrencia)
            .SingleOrDefaultAsync(cancellationToken);
        if (todo is null)
        {
            return new ServiceResult<TodoResponse>(null, NotFound: true);
        }

        var category = await GetOwnedCategoryAsync(userId, categoriaId, cancellationToken);
        if (categoriaId is not null && category is null)
            return new ServiceResult<TodoResponse>(null, "Categoria inválida para o usuário autenticado.");
        if (prioridade is not null && !Enum.IsDefined(prioridade.Value))
            return new ServiceResult<TodoResponse>(null, "Prioridade inválida.");

        var windowError = SchedulingService.ValidateWindow(dataInicioUtc, dataFimUtc, out var startUtc, out var endUtc);
        if (windowError is not null) return new ServiceResult<TodoResponse>(null, windowError);
        var timezoneId = await dbContext.Users.Where(x => x.Id == userId).Select(x => x.TimeZoneId).SingleAsync(cancellationToken);
        if (startUtc is not null && !SchedulingService.IsSameLocalDate(startUtc.Value, endUtc!.Value, timezoneId))
            return new ServiceResult<TodoResponse>(null, "A janela deve começar e terminar na mesma data local.");
        var proposedSeries = BuildProposedSeries(todo, userId, titulo.Trim(), descricao.Trim(), frequenciaRecorrencia,
            duracaoRecorrencia, startUtc, endUtc, timezoneId);
        var proposedWindows = BuildCandidateWindows(startUtc, endUtc, proposedSeries, timezoneId, todo.NumeroOcorrencia);
        if (proposedWindows.Count > 0 && await schedulingService.FindConflictAsync(userId, timezoneId, proposedWindows, id, cancellationToken) is not null)
            return new ServiceResult<TodoResponse>(null, "A janela conflita com outro horÃ¡rio agendado.", Conflict: true);

        var wasCompleted = todo.Concluido;
        var wasRecurring = todo.SerieRecorrencia is not null;
        var nowUtc = DateTime.UtcNow;
        var validationError = recurrenceService.UpdateConfiguration(
            todo,
            titulo.Trim(),
            descricao.Trim(),
            categoriaId,
            prioridade ?? Prioridade.Media,
            frequenciaRecorrencia,
            duracaoRecorrencia,
            nowUtc);
        if (validationError is not null)
        {
            return new ServiceResult<TodoResponse>(null, validationError);
        }

        todo.DataInicioUtc = startUtc;
        todo.DataFimUtc = endUtc;
        if (todo.SerieRecorrencia is { } proposedRecurringSeries)
        {
            proposedRecurringSeries.ScheduleAnchorLocalDate = proposedSeries?.ScheduleAnchorLocalDate;
            proposedRecurringSeries.ScheduleAnchorOccurrenceNumber = proposedSeries?.ScheduleAnchorOccurrenceNumber;
            proposedRecurringSeries.ScheduleStartLocalTime = proposedSeries?.ScheduleStartLocalTime;
            proposedRecurringSeries.ScheduleEndLocalTime = proposedSeries?.ScheduleEndLocalTime;
        }

        if (concluido && !todo.Concluido)
        {
            todo.DataConclusaoUtc = nowUtc;
            if (todo.SerieRecorrencia is { IsActive: true } series
                && todo.NumeroOcorrencia is not null
                && series.NextOccurrenceNumber == todo.NumeroOcorrencia.Value + 1)
            {
                recurrenceService.CreateNextOccurrence(series, nowUtc, timezoneId);
            }
        }
        else if (!concluido)
        {
            todo.DataConclusaoUtc = null;
        }
        else if (!wasRecurring && todo.SerieRecorrencia is { IsActive: true } newlyCreatedSeries
                 && todo.NumeroOcorrencia is not null
                 && newlyCreatedSeries.NextOccurrenceNumber == todo.NumeroOcorrencia.Value + 1)
        {
            recurrenceService.CreateNextOccurrence(newlyCreatedSeries, nowUtc, timezoneId);
        }

        todo.Concluido = concluido;
        todo.Categoria = category;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ServiceResult<TodoResponse>(await ToResponseAsync(todo, cancellationToken));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int userId, int id, string? scope, CancellationToken cancellationToken)
    {
        var todo = await FindOwnedTodo(userId, id)
            .Include(item => item.SerieRecorrencia)
            .SingleOrDefaultAsync(cancellationToken);
        if (todo is null)
        {
            return new ServiceResult<bool>(false, NotFound: true);
        }

        if (todo.SerieRecorrenciaId is null)
        {
            if (scope is not null)
            {
                return new ServiceResult<bool>(false, "scope só é válido para uma ocorrência recorrente.");
            }

            dbContext.Todos.Remove(todo);
        }
        else if (scope == "occurrence")
        {
            dbContext.Todos.Remove(todo);
        }
        else if (scope == "series")
        {
            var series = await dbContext.RecurrenceSeries.SingleOrDefaultAsync(
                item => item.Id == todo.SerieRecorrenciaId && item.UserId == userId,
                cancellationToken);
            if (series is null)
            {
                return new ServiceResult<bool>(false, NotFound: true);
            }

            dbContext.RecurrenceSeries.Remove(series);
        }
        else
        {
            return new ServiceResult<bool>(false, "Uma ocorrência recorrente exige scope=occurrence ou scope=series.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ServiceResult<bool>(true);
    }

    public async Task<ServiceResult<bool>> DeleteSeriesAsync(int userId, int seriesId, CancellationToken cancellationToken)
    {
        var series = await dbContext.RecurrenceSeries.SingleOrDefaultAsync(
            item => item.Id == seriesId && item.UserId == userId,
            cancellationToken);
        if (series is null)
        {
            return new ServiceResult<bool>(false, NotFound: true);
        }

        dbContext.RecurrenceSeries.Remove(series);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ServiceResult<bool>(true);
    }

    private IQueryable<TodoItem> FindOwnedTodo(int userId, int id) =>
        dbContext.Todos.Where(todo => todo.Id == id && todo.UserId == userId);

    private Task<Categoria?> GetOwnedCategoryAsync(int userId, int? categoryId, CancellationToken cancellationToken) =>
        categoryId is null ? Task.FromResult<Categoria?>(null) : dbContext.Categorias.SingleOrDefaultAsync(
            category => category.Id == categoryId && category.UserId == userId, cancellationToken);

    private async Task<TodoResponse> ToResponseAsync(TodoItem todo, CancellationToken cancellationToken)
    {
        if (todo.SerieRecorrenciaId is not null && todo.SerieRecorrencia is null)
        {
            await dbContext.Entry(todo).Reference(item => item.SerieRecorrencia).LoadAsync(cancellationToken);
        }
        if (todo.CategoriaId is not null && todo.Categoria is null)
        {
            await dbContext.Entry(todo).Reference(item => item.Categoria).LoadAsync(cancellationToken);
        }

        return ToResponse(todo);
    }

    private static TodoResponse ToResponse(TodoItem todo) =>
        new(
            todo.Id,
            todo.Titulo,
            todo.Descricao,
            todo.CategoriaId,
            todo.Categoria?.Nome,
            todo.Prioridade,
            DateTime.SpecifyKind(todo.DataCriacao, DateTimeKind.Utc),
            todo.Concluido,
            todo.DataConclusaoUtc is null ? null : DateTime.SpecifyKind(todo.DataConclusaoUtc.Value, DateTimeKind.Utc),
            todo.DataPrevistaUtc is null ? null : DateTime.SpecifyKind(todo.DataPrevistaUtc.Value, DateTimeKind.Utc),
            todo.DataInicioUtc is null ? null : DateTime.SpecifyKind(todo.DataInicioUtc.Value, DateTimeKind.Utc),
            todo.DataFimUtc is null ? null : DateTime.SpecifyKind(todo.DataFimUtc.Value, DateTimeKind.Utc),
            todo.SerieRecorrencia?.Frequency,
            todo.SerieRecorrencia?.Duration,
            todo.SerieRecorrenciaId,
            todo.NumeroOcorrencia);

    private static RecurrenceSeries? BuildProposedSeries(TodoItem? todo, int userId, string title, string description,
        RecurrenceFrequency? frequency, int? duration, DateTime? start, DateTime? end, string timeZoneId)
    {
        if (frequency is null || duration is null || start is null || end is null) return null;
        var anchor = SchedulingService.ToLocalAnchor(start.Value, end.Value, timeZoneId);
        return new RecurrenceSeries
        {
            Id = todo?.SerieRecorrencia?.Id ?? 0,
            UserId = userId,
            TitleBase = title,
            DescriptionBase = description,
            Frequency = frequency,
            Duration = duration,
            IsActive = true,
            NextOccurrenceNumber = todo?.SerieRecorrencia?.NextOccurrenceNumber ?? 2,
            LastOccurrenceDueUtc = todo?.SerieRecorrencia?.LastOccurrenceDueUtc ?? DateTime.UtcNow,
            NextDataPrevistaUtc = todo?.SerieRecorrencia?.NextDataPrevistaUtc ?? DateTime.UtcNow,
            ScheduleAnchorLocalDate = anchor.Date,
            ScheduleAnchorOccurrenceNumber = todo?.NumeroOcorrencia ?? 1,
            ScheduleStartLocalTime = anchor.Start,
            ScheduleEndLocalTime = anchor.End
        };
    }

    private static List<ScheduledWindow> BuildCandidateWindows(DateTime? start, DateTime? end, RecurrenceSeries? series, string timeZoneId, int? currentOccurrenceNumber = null)
    {
        if (start is null || end is null) return [];
        var windows = new List<ScheduledWindow> { new(start.Value, end.Value, SeriesId: series is { Id: > 0 } ? series.Id : null, OccurrenceNumber: currentOccurrenceNumber) };
        if (series?.Frequency is null || series.Duration is null || series.ScheduleAnchorLocalDate is null) return windows;
        for (var number = series.NextOccurrenceNumber; number <= series.Duration; number++)
            windows.Add(SchedulingService.Project(series, timeZoneId, number));
        return windows;
    }
}
