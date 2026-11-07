using Microsoft.EntityFrameworkCore;
using TodoAPI.Domain.Entities;
using TodoAPI.Domain.Services;
using TodoAPI.Infrastructure.Persistence;

namespace TodoAPI.Application.Services;

public sealed class RecurrenceService(TodoDbContext dbContext, SchedulingService schedulingService, ILogger<RecurrenceService> logger)
{
    public static string? ValidateConfiguration(RecurrenceFrequency? frequency, int? duration)
    {
        if (frequency is null && duration is null)
        {
            return null;
        }

        if (frequency is null)
        {
            return "Frequência é obrigatória quando a duração é informada.";
        }

        if (!Enum.IsDefined(frequency.Value))
        {
            return "Frequência de recorrência inválida.";
        }

        if (duration is null or <= 0)
        {
            return "Duração de recorrência deve ser um inteiro positivo.";
        }

        var maximum = frequency.Value switch
        {
            RecurrenceFrequency.Diaria => 3650,
            RecurrenceFrequency.Semanal => 520,
            RecurrenceFrequency.Mensal => 120,
            _ => 0
        };
        if (duration.Value > maximum) return $"Duração máxima para essa frequência: {maximum}.";

        return null;
    }

    public void AttachNewSeries(TodoItem todo, int userId, RecurrenceFrequency frequency, int duration, DateTime nowUtc)
    {
        var series = new RecurrenceSeries
        {
            UserId = userId,
            DescriptionBase = todo.Descricao,
            TitleBase = todo.Titulo,
            CategoryIdBase = todo.CategoriaId,
            PriorityBase = todo.Prioridade,
            Frequency = frequency,
            Duration = duration,
            IsActive = duration > 1,
            NextOccurrenceNumber = 2,
            LastOccurrenceDueUtc = nowUtc,
            NextDataPrevistaUtc = RecurrenceSchedule.CalculateNextDueUtc(nowUtc, frequency)
        };

        todo.SerieRecorrencia = series;
        todo.NumeroOcorrencia = 1;
        todo.DataPrevistaUtc = nowUtc;
        dbContext.RecurrenceSeries.Add(series);
    }

    public string? UpdateConfiguration(
        TodoItem todo,
        string title,
        string description,
        int? categoryId,
        Prioridade priority,
        RecurrenceFrequency? frequency,
        int? duration,
        DateTime nowUtc)
    {
        var validationError = ValidateConfiguration(frequency, duration);
        if (validationError is not null)
        {
            return validationError;
        }

        if (frequency is null || duration is null)
        {
            if (todo.SerieRecorrencia is { } existingSeries)
            {
                existingSeries.IsActive = false;
                existingSeries.Frequency = null;
                existingSeries.Duration = null;
                existingSeries.DescriptionBase = description;
                existingSeries.TitleBase = title;
                existingSeries.CategoryIdBase = categoryId;
                existingSeries.PriorityBase = priority;
            }

            todo.Descricao = description;
            todo.Titulo = title;
            todo.CategoriaId = categoryId;
            todo.Prioridade = priority;
            return null;
        }

        if (todo.SerieRecorrencia is null)
        {
            todo.Descricao = description;
            todo.Titulo = title;
            todo.CategoriaId = categoryId;
            todo.Prioridade = priority;
            AttachNewSeries(todo, todo.UserId, frequency.Value, duration.Value, nowUtc);
            return null;
        }

        var series = todo.SerieRecorrencia;
        var highestOccurrenceNumber = series.NextOccurrenceNumber - 1;
        if (duration.Value < highestOccurrenceNumber)
        {
            return "A duração não pode ser menor que o maior número de ocorrência já gerado.";
        }

        todo.Descricao = description;
        todo.Titulo = title;
        todo.CategoriaId = categoryId;
        todo.Prioridade = priority;
        series.TitleBase = title;
        series.CategoryIdBase = categoryId;
        series.PriorityBase = priority;
        series.DescriptionBase = description;
        series.Frequency = frequency.Value;
        series.Duration = duration.Value;
        series.NextDataPrevistaUtc = RecurrenceSchedule.CalculateNextDueUtc(series.LastOccurrenceDueUtc, frequency.Value);
        series.IsActive = series.NextOccurrenceNumber <= duration.Value;
        return null;
    }

    public TodoItem? CreateNextOccurrence(RecurrenceSeries series, DateTime createdAtUtc, string? timeZoneId = null)
    {
        if (!series.IsActive || series.Frequency is null || series.Duration is null
            || series.NextOccurrenceNumber > series.Duration.Value)
        {
            series.IsActive = false;
            return null;
        }

        var dueUtc = DateTime.SpecifyKind(series.NextDataPrevistaUtc, DateTimeKind.Utc);
        var todo = new TodoItem
        {
            UserId = series.UserId,
            Descricao = series.DescriptionBase,
            Titulo = series.TitleBase,
            CategoriaId = series.CategoryIdBase,
            Prioridade = series.PriorityBase,
            DataCriacao = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc),
            DataPrevistaUtc = dueUtc,
            Concluido = false,
            SerieRecorrencia = series,
            NumeroOcorrencia = series.NextOccurrenceNumber
        };
        if (timeZoneId is not null && series.ScheduleAnchorLocalDate is not null
            && series.ScheduleStartLocalTime is not null && series.ScheduleEndLocalTime is not null
            && series.Frequency is not null && series.Duration is not null)
        {
            var window = SchedulingService.Project(series, timeZoneId, series.NextOccurrenceNumber);
            todo.DataInicioUtc = window.StartUtc;
            todo.DataFimUtc = window.EndUtc;
        }

        series.LastOccurrenceDueUtc = dueUtc;
        series.NextOccurrenceNumber++;
        series.NextDataPrevistaUtc = RecurrenceSchedule.CalculateNextDueUtc(dueUtc, series.Frequency!.Value);
        if (series.NextOccurrenceNumber > series.Duration!.Value)
        {
            series.IsActive = false;
        }

        dbContext.Todos.Add(todo);
        return todo;
    }

    public async Task<int> ProcessDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var dueSeriesIds = await dbContext.RecurrenceSeries
            .AsNoTracking()
            .Where(series => series.IsActive && series.NextDataPrevistaUtc <= nowUtc)
            .OrderBy(series => series.NextDataPrevistaUtc)
            .Select(series => series.Id)
            .ToListAsync(cancellationToken);

        var generatedCount = 0;
        foreach (var seriesId in dueSeriesIds)
        {
            var series = await dbContext.RecurrenceSeries.SingleOrDefaultAsync(
                candidate => candidate.Id == seriesId,
                cancellationToken);
            if (series is null)
            {
                continue;
            }

            var ownerTimeZoneId = await dbContext.Users.Where(user => user.Id == series.UserId)
                .Select(user => user.TimeZoneId).SingleAsync(cancellationToken);
            while (series.IsActive
                   && series.Frequency is not null
                   && series.Duration is not null
                   && series.NextOccurrenceNumber <= series.Duration.Value
                   && series.NextDataPrevistaUtc <= nowUtc)
            {
                if (series.ScheduleAnchorLocalDate is not null && series.ScheduleStartLocalTime is not null
                    && series.ScheduleEndLocalTime is not null)
                {
                    var projected = SchedulingService.Project(series, ownerTimeZoneId, series.NextOccurrenceNumber);
                    var occupied = await schedulingService.GetWindowsAsync(series.UserId, ownerTimeZoneId, ct: cancellationToken);
                    var conflict = occupied.FirstOrDefault(window =>
                        !(window.SeriesId == series.Id && window.OccurrenceNumber == series.NextOccurrenceNumber)
                        && SchedulingService.Overlaps(projected, window));
                    if (conflict is not null)
                    {
                        series.IsActive = false;
                        logger.LogError("Recurring schedule conflict for series {SeriesId}, occurrence {OccurrenceNumber}, window {StartUtc}-{EndUtc}",
                            series.Id, series.NextOccurrenceNumber, projected.StartUtc, projected.EndUtc);
                        break;
                    }
                }

                if (CreateNextOccurrence(series, nowUtc, ownerTimeZoneId) is null)
                {
                    break;
                }

                generatedCount++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }

        return generatedCount;
    }
}
