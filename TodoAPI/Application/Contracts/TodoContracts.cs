using System.ComponentModel.DataAnnotations;
using TodoAPI.Domain.Entities;

namespace TodoAPI.Application.Contracts;

public sealed class CreateTodoRequest
{
    [Required]
    [NotWhiteSpace]
    [StringLength(200)]
    public string Titulo { get; init; } = string.Empty;

    [Required]
    [NotWhiteSpace]
    [StringLength(200)]
    public string Descricao { get; init; } = string.Empty;

    public bool Concluido { get; init; }

    public int? CategoriaId { get; init; }

    public Prioridade? Prioridade { get; init; }

    public RecurrenceFrequency? FrequenciaRecorrencia { get; init; }

    public int? DuracaoRecorrencia { get; init; }

    public DateTimeOffset? DataInicioUtc { get; init; }

    public DateTimeOffset? DataFimUtc { get; init; }
}

public sealed class UpdateTodoRequest
{
    [Required]
    [NotWhiteSpace]
    [StringLength(200)]
    public string Titulo { get; init; } = string.Empty;

    [Required]
    [NotWhiteSpace]
    [StringLength(200)]
    public string Descricao { get; init; } = string.Empty;

    public bool Concluido { get; init; }

    public int? CategoriaId { get; init; }

    public Prioridade? Prioridade { get; init; }

    public RecurrenceFrequency? FrequenciaRecorrencia { get; init; }

    public int? DuracaoRecorrencia { get; init; }

    public DateTimeOffset? DataInicioUtc { get; init; }

    public DateTimeOffset? DataFimUtc { get; init; }
}

public sealed record TodoResponse(
    int Id,
    string Titulo,
    string Descricao,
    int? CategoriaId,
    string? CategoriaNome,
    Prioridade Prioridade,
    DateTime DataCriacao,
    bool Concluido,
    DateTime? DataConclusaoUtc,
    DateTime? DataPrevistaUtc,
    DateTime? DataInicioUtc,
    DateTime? DataFimUtc,
    RecurrenceFrequency? FrequenciaRecorrencia,
    int? DuracaoRecorrencia,
    int? SerieRecorrenciaId,
    int? NumeroOcorrencia);

public sealed record TodoPageResponse(IReadOnlyList<TodoResponse> Items, int Page, int PageSize, int TotalCount);

public sealed class CreateCategoriaRequest
{
    [Required]
    [NotWhiteSpace]
    public string Nome { get; init; } = string.Empty;
}

public sealed class UpdateCategoriaRequest
{
    [Required]
    [NotWhiteSpace]
    public string Nome { get; init; } = string.Empty;
}

public sealed record CategoriaResponse(int Id, string Nome);

public sealed class UpdatePreferenceRequest
{
    [Required]
    [NotWhiteSpace]
    public string TimeZoneId { get; init; } = string.Empty;
}

public sealed record PreferenceResponse(string TimeZoneId);
