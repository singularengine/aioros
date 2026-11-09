using Npgsql;
using ViewScale.Data;

var builder = AiorosApplication.CreateBuilder(args);
builder.Server(services =>
{
    if (Environment.GetEnvironmentVariable("VIEWSCALE_DATA") == "postgres")
    {
        var connection = Environment.GetEnvironmentVariable("VIEWSCALE_PG")
            ?? throw new InvalidOperationException("VIEWSCALE_PG is required with VIEWSCALE_DATA=postgres.");
        services.AddSingleton(NpgsqlDataSource.Create(connection));
        services.AddSingleton<IRowSource, PostgresRows>();
    }
    else
    {
        services.AddSingleton<IRowSource, MemoryRows>();
    }
});

var app = builder.Build();
app.MapAioros();
return app.RunAioros();
