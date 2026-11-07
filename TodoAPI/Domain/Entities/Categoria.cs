namespace TodoAPI.Domain.Entities;

public sealed class Categoria
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public ICollection<TodoItem> Todos { get; set; } = new List<TodoItem>();
}
