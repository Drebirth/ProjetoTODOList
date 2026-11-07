namespace TodoAPI.Domain.Entities;

public sealed class User
{
    public int Id { get; set; }

    public required string Username { get; set; }

    public required string NormalizedUsername { get; set; }

    public required string PasswordHash { get; set; }

    public string TimeZoneId { get; set; } = "America/Fortaleza";

    public ICollection<TodoItem> Todos { get; set; } = new List<TodoItem>();

    public ICollection<RecurrenceSeries> RecurrenceSeries { get; set; } = new List<RecurrenceSeries>();

    public ICollection<Categoria> Categorias { get; set; } = new List<Categoria>();
}
