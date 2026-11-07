using Microsoft.EntityFrameworkCore;
using TodoAPI.Application.Contracts;
using TodoAPI.Domain.Entities;
using TodoAPI.Infrastructure.Persistence;

namespace TodoAPI.Application.Services;

public sealed class CategoriaService(TodoDbContext db)
{
    public async Task<IReadOnlyList<CategoriaResponse>> GetAllAsync(int userId, CancellationToken ct) =>
        await db.Categorias.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.Id)
            .Select(x => new CategoriaResponse(x.Id, x.Nome)).ToListAsync(ct);

    public async Task<CategoriaResponse?> GetByIdAsync(int userId, int id, CancellationToken ct) =>
        await db.Categorias.AsNoTracking().Where(x => x.Id == id && x.UserId == userId)
            .Select(x => new CategoriaResponse(x.Id, x.Nome)).SingleOrDefaultAsync(ct);

    public async Task<CategoriaResponse> CreateAsync(int userId, string nome, CancellationToken ct)
    {
        var category = new Categoria { UserId = userId, Nome = nome.Trim() };
        db.Categorias.Add(category);
        await db.SaveChangesAsync(ct);
        return new CategoriaResponse(category.Id, category.Nome);
    }

    public async Task<CategoriaResponse?> UpdateAsync(int userId, int id, string nome, CancellationToken ct)
    {
        var category = await db.Categorias.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (category is null) return null;
        category.Nome = nome.Trim();
        await db.SaveChangesAsync(ct);
        return new CategoriaResponse(category.Id, category.Nome);
    }

    public async Task<(bool Found, bool InUse)> DeleteAsync(int userId, int id, CancellationToken ct)
    {
        var category = await db.Categorias.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (category is null) return (false, false);
        if (await db.Todos.AnyAsync(x => x.CategoriaId == id && x.UserId == userId, ct)
            || await db.RecurrenceSeries.AnyAsync(x => x.CategoryIdBase == id && x.UserId == userId, ct))
            return (true, true);
        db.Categorias.Remove(category);
        await db.SaveChangesAsync(ct);
        return (true, false);
    }
}
