using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Showcase.Games;

public static class GamesSetup
{
    /// <summary>
    /// The games store: PostgreSQL when the Aspire AppHost provides the <c>games</c> connection string (with Aspire's
    /// Npgsql integration: retries on transient failures, a health check, tracing and metrics), otherwise a
    /// SQLite file next to the app, so it also runs with nothing installed. ASP.NET Core Identity keeps its users in
    /// the same database. The database, its seeder and the virtual players are the server's: the installed app working
    /// against a remote server registers none of them, and signs in through the system browser instead
    /// (<c>AddInstalledAppSignIn</c>), on the web's <c>/login</c> page.
    /// </summary>
    public static WebApplicationBuilder AddGames(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddOutputCache();
        builder.AddServerFunction<IGameStore, GameStore>("games");
        builder.AddServerFunction<ILabFunctions, LabFunctions>();
        builder.AddServerFunction<IAccountFunctions, AccountFunctions>("account");
        builder.Server(services => AddServer(builder, services));
        return builder.AddInstalledAppSignIn(static stores => stores.UseEntityFrameworkCore().UseDbContext<SignInDb>());
    }

    private static void AddServer(WebApplicationBuilder builder, IServiceCollection services)
    {
        var postgres = builder.Configuration.GetConnectionString("games");
        GamesOptions options;
        if (postgres is { Length: > 0 })
        {
            var connection = new NpgsqlConnectionStringBuilder(postgres) { MaxPoolSize = 64 }.ConnectionString;
            services.AddPooledDbContextFactory<GamesDb>(database => database.UseNpgsql(connection));
            builder.EnrichNpgsqlDbContext<GamesDb>();
            options = new GamesOptions("PostgreSQL", Math.Clamp(Environment.ProcessorCount / 2, 2, 8), 1, [0, 16, 64, 256, 512]);
        }
        else
        {
            var file = Path.Combine(builder.Environment.ContentRootPath, "games.db");
            services.AddPooledDbContextFactory<GamesDb>(database => database.UseSqlite($"Data Source={file};Default Timeout=30"));
            options = new GamesOptions("SQLite", 1, 0.25, [0, 2, 4, 8]);
        }

        services.AddSingleton(options);
        services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<GamesDb>>().CreateDbContext());
        services.AddSingleton<SeedProgress>();
        services.AddHostedService<GameSeeder>();
        services.AddSingleton<StoreTraffic>();
        services.AddHostedService(provider => provider.GetRequiredService<StoreTraffic>());
        services.AddHttpContextAccessor();
        services.AddHostedService<LabFeed>();
        AddAccounts(services);
        SignInDb.Add(builder, services);
    }

    /// <summary>Serves the game art; on the desktop, <see cref="GameArtSource"/> draws it in process instead.</summary>
    public static WebApplication MapGameArt(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseOutputCache();
        if (app.Services.GetService<StoreTraffic>() is not null)
        {
            UseServedThreads(app);
        }

        app.MapGet(GameArt.Prefix + "{id:int}/{kind}.png", (HttpContext http, int id, string kind) =>
        {
            if (!GameArt.TryParse(http.Request.Path.Value ?? string.Empty, out _, out _))
            {
                return Results.NotFound();
            }

            http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Results.File(GameArt.Render(id, kind), "image/png");
        }).CacheOutput(policy => policy.Expire(TimeSpan.FromDays(7)));
        return app;
    }

    private static void UseServedThreads(WebApplication app)
    {
        var traffic = app.Services.GetRequiredService<StoreTraffic>();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/games", StringComparison.Ordinal))
            {
                traffic.Served();
            }

            await next();
        });
    }

    private static void AddAccounts(IServiceCollection services)
    {
        services.AddIdentityCore<IdentityUser>(identity =>
            {
                identity.Password.RequiredLength = 4;
                identity.Password.RequireDigit = false;
                identity.Password.RequireLowercase = false;
                identity.Password.RequireUppercase = false;
                identity.Password.RequireNonAlphanumeric = false;
                identity.Password.RequiredUniqueChars = 1;
            })
            .AddSignInManager()
            .AddEntityFrameworkStores<GamesDb>();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(cookie =>
        {
            cookie.LoginPath = "/login";
            cookie.LogoutPath = "/logout";
        });
        services.AddAuthorization();
    }
}
