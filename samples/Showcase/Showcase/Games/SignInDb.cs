using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Showcase.Games;

/// <summary>
/// Where the installed app's sign-in keeps its client, authorizations and tokens (OpenIddict's EF Core store): the
/// <c>signin</c> PostgreSQL database when the Aspire AppHost provides it, otherwise a SQLite file next to the app.
/// </summary>
public sealed class SignInDb(DbContextOptions<SignInDb> options) : DbContext(options)
{
    public static void Add(WebApplicationBuilder builder, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var postgres = builder.Configuration.GetConnectionString("signin");
        var file = Path.Combine(builder.Environment.ContentRootPath, "signin.db");
        services.AddDbContext<SignInDb>(database =>
        {
            if (postgres is { Length: > 0 })
            {
                database.UseNpgsql(new NpgsqlConnectionStringBuilder(postgres).ConnectionString);
            }
            else
            {
                database.UseSqlite($"Data Source={file}");
            }

            database.UseOpenIddict();
        });
        if (postgres is { Length: > 0 })
        {
            builder.EnrichNpgsqlDbContext<SignInDb>();
        }

        services.AddHostedService<Schema>();
    }

    /// <summary>Creates the tables before the sign-in registers its client.</summary>
    private sealed class Schema(IServiceScopeFactory scopes) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<SignInDb>().Database.EnsureCreatedAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
