namespace TodoAPI.Domain.Entities;

public sealed class TodoItem
{
    public int Id { get; set; }

    public required string Descricao { get; set; }

    public required string Titulo { get; set; }

    public int? CategoriaId { get; set; }

    public Categoria? Categoria { get; set; }

    public Prioridade Prioridade { get; set; } = Prioridade.Media;

    public DateTime DataCriacao { get; set; }

    public DateTime? DataConclusaoUtc { get; set; }

    public DateTime? DataPrevistaUtc { get; set; }

    public DateTime? DataInicioUtc { get; set; }

    public DateTime? DataFimUtc { get; set; }

    public int? SerieRecorrenciaId { get; set; }

    public RecurrenceSeries? SerieRecorrencia { get; set; }

    public int? NumeroOcorrencia { get; set; }

    public bool Concluido { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }
}
