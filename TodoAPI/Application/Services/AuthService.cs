using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TodoAPI.Domain.Entities;
using TodoAPI.Infrastructure.Persistence;

namespace TodoAPI.Application.Services;

public sealed class AuthService(TodoDbContext dbContext, IPasswordHasher<User> passwordHasher)
{
    public async Task<(User? User, bool UsernameAlreadyExists)> RegisterAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeUsername(username);
        if (await dbContext.Users.AnyAsync(user => user.NormalizedUsername == normalizedUsername, cancellationToken))
        {
            return (null, true);
        }

        var user = new User
        {
            Username = username.Trim(),
            NormalizedUsername = normalizedUsername,
            PasswordHash = string.Empty
        };
        foreach (var name in new[] { "Estudos", "Reunião", "Trabalho", "Pessoal" })
        {
            user.Categorias.Add(new Categoria { Nome = name, User = user });
        }
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return (user, false);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(user).State = EntityState.Detached;
            if (await dbContext.Users.AnyAsync(existing => existing.NormalizedUsername == normalizedUsername, cancellationToken))
            {
                return (null, true);
            }

            throw;
        }
    }

    public async Task<User?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeUsername(username);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.NormalizedUsername == normalizedUsername,
            cancellationToken);
        if (user is null)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return verification == PasswordVerificationResult.Failed ? null : user;
    }

    private static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();
}
