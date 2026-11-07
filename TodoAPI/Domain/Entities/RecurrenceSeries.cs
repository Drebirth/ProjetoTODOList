namespace TodoAPI.Domain.Entities;

public sealed class RecurrenceSeries
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public required string DescriptionBase { get; set; }

    public required string TitleBase { get; set; }

    public int? CategoryIdBase { get; set; }

    public Prioridade PriorityBase { get; set; } = Prioridade.Media;

    public RecurrenceFrequency? Frequency { get; set; }

    public int? Duration { get; set; }

    public bool IsActive { get; set; }

    public int NextOccurrenceNumber { get; set; }

    public DateTime LastOccurrenceDueUtc { get; set; }

    public DateTime NextDataPrevistaUtc { get; set; }

    public DateOnly? ScheduleAnchorLocalDate { get; set; }

    public int? ScheduleAnchorOccurrenceNumber { get; set; }

    public TimeOnly? ScheduleStartLocalTime { get; set; }

    public TimeOnly? ScheduleEndLocalTime { get; set; }

    public ICollection<TodoItem> Occurrences { get; set; } = new List<TodoItem>();
}
