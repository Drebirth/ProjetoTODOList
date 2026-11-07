using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TodoAPI.Domain.Entities;

namespace TodoAPI.Infrastructure.Persistence;

public sealed class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<TodoItem> Todos => Set<TodoItem>();

    public DbSet<RecurrenceSeries> RecurrenceSeries => Set<RecurrenceSeries>();

    public DbSet<Categoria> Categorias => Set<Categoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username).HasMaxLength(50).IsRequired();
            entity.Property(user => user.NormalizedUsername).HasMaxLength(50).IsRequired();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.Property(user => user.TimeZoneId).HasDefaultValue("America/Fortaleza").IsRequired();
            entity.HasIndex(user => user.NormalizedUsername).IsUnique();
            entity.HasMany(user => user.Todos)
                .WithOne(todo => todo.User)
                .HasForeignKey(todo => todo.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(user => user.RecurrenceSeries)
                .WithOne(series => series.User)
                .HasForeignKey(series => series.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecurrenceSeries>(entity =>
        {
            entity.HasKey(series => series.Id);
            entity.Property(series => series.DescriptionBase).HasMaxLength(200).IsRequired();
            entity.Property(series => series.Frequency).HasConversion<string>();
            entity.Property(series => series.PriorityBase).HasConversion<string>();
            entity.Property(series => series.ScheduleAnchorLocalDate).HasConversion<string>();
            entity.Property(series => series.ScheduleAnchorOccurrenceNumber);
            entity.Property(series => series.ScheduleStartLocalTime).HasConversion<string>();
            entity.Property(series => series.ScheduleEndLocalTime).HasConversion<string>();
            entity.HasOne<Categoria>().WithMany().HasForeignKey(series => series.CategoryIdBase).OnDelete(DeleteBehavior.Restrict);
            entity.Property(series => series.IsActive).IsRequired();
            entity.Property(series => series.NextOccurrenceNumber).IsRequired();
            entity.Property(series => series.LastOccurrenceDueUtc)
                .HasConversion(new ValueConverter<DateTime, long>(
                    value => value.ToUniversalTime().Ticks,
                    value => new DateTime(value, DateTimeKind.Utc)))
                .HasColumnType("INTEGER")
                .IsRequired();
            entity.Property(series => series.NextDataPrevistaUtc)
                .HasConversion(new ValueConverter<DateTime, long>(
                    value => value.ToUniversalTime().Ticks,
                    value => new DateTime(value, DateTimeKind.Utc)))
                .HasColumnType("INTEGER")
                .IsRequired();
            entity.HasMany(series => series.Occurrences)
                .WithOne(todo => todo.SerieRecorrencia)
                .HasForeignKey(todo => todo.SerieRecorrenciaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.HasKey(todo => todo.Id);
            entity.Property(todo => todo.Descricao).HasMaxLength(200).IsRequired();
            entity.Property(todo => todo.Titulo).HasMaxLength(200).IsRequired();
            entity.Property(todo => todo.Prioridade).HasConversion<string>().IsRequired();
            entity.HasOne(todo => todo.Categoria).WithMany(category => category.Todos)
                .HasForeignKey(todo => todo.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(todo => todo.DataCriacao)
                .HasConversion(new ValueConverter<DateTime, long>(
                    value => value.ToUniversalTime().Ticks,
                    value => new DateTime(value, DateTimeKind.Utc)))
                .HasColumnType("INTEGER")
                .IsRequired();
            entity.Property(todo => todo.DataConclusaoUtc)
                .HasConversion(
                    value => value.HasValue ? value.Value.ToUniversalTime().Ticks : (long?)null,
                    value => value.HasValue ? new DateTime(value.Value, DateTimeKind.Utc) : (DateTime?)null)
                .HasColumnType("INTEGER");
            entity.Property(todo => todo.DataPrevistaUtc)
                .HasConversion(
                    value => value.HasValue ? value.Value.ToUniversalTime().Ticks : (long?)null,
                    value => value.HasValue ? new DateTime(value.Value, DateTimeKind.Utc) : (DateTime?)null)
                .HasColumnType("INTEGER");
            entity.Property(todo => todo.DataInicioUtc)
                .HasConversion(value => value.HasValue ? value.Value.ToUniversalTime().Ticks : (long?)null,
                    value => value.HasValue ? new DateTime(value.Value, DateTimeKind.Utc) : (DateTime?)null)
                .HasColumnType("INTEGER");
            entity.Property(todo => todo.DataFimUtc)
                .HasConversion(value => value.HasValue ? value.Value.ToUniversalTime().Ticks : (long?)null,
                    value => value.HasValue ? new DateTime(value.Value, DateTimeKind.Utc) : (DateTime?)null)
                .HasColumnType("INTEGER");
            entity.Property(todo => todo.Concluido).IsRequired();
            entity.HasIndex(todo => new { todo.UserId, todo.DataCriacao, todo.Id });
            entity.HasIndex(todo => new { todo.SerieRecorrenciaId, todo.NumeroOcorrencia }).IsUnique();
        });

        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Nome).IsRequired();
            entity.HasOne(category => category.User).WithMany(user => user.Categorias)
                .HasForeignKey(category => category.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
