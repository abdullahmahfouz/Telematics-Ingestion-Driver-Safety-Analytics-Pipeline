using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TelematicsPipeline.Api.Caching;
using TelematicsPipeline.Api.Persistence;
using TelematicsPipeline.Api.Services;

const string DashboardCorsPolicy = "DashboardCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<TelematicsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TelematicsDb")));

builder.Services.AddScoped<TelematicsQueryService>();

// Redis is registered as optional. The leaderboard it backs is derived data -- Postgres
// stays the source of truth -- so a Redis outage must degrade that one feature rather
// than stopping the API from starting or blocking ingestion.
builder.Services.AddSingleton<IConnectionMultiplexer?>(serviceProvider =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 2000;

        return ConnectionMultiplexer.Connect(options);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Redis unavailable at {ConnectionString}; leaderboard disabled", connectionString);
        return null;
    }
});

builder.Services.AddScoped<SafetyLeaderboard>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(DashboardCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(DashboardCorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();
