using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using TelematicsPipeline.Api.Auth;
using TelematicsPipeline.Api.Caching;
using TelematicsPipeline.Api.Models;
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
builder.Services.AddSingleton<JwtTokenService>();

var jwtSigningKey = JwtTokenService.SigningKeyOrThrow(builder.Configuration);
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "telematics-api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "telematics-dashboard";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Without this, the handler silently remaps well-known claims (e.g. "sub" ->
        // ClaimTypes.NameIdentifier) on validation, which surprises anyone reading claims
        // back out by their JWT name later.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationDefaults.Scheme, _ => { });

builder.Services.AddAuthorization();

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

    // Dev convenience only: applies pending migrations and seeds one demo login so a fresh
    // clone works with `dotnet run` alone. Never runs outside Development.
    await InitializeDevelopmentDatabaseAsync(app.Services);
}

app.UseHttpsRedirection();

app.UseCors(DashboardCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static async Task InitializeDevelopmentDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TelematicsDbContext>();

    await db.Database.MigrateAsync();

    const string demoUsername = "demo";
    const string demoPassword = "DemoPass123!";
    if (!await db.Users.AnyAsync(u => u.Username == demoUsername))
    {
        db.Users.Add(new User
        {
            Username = demoUsername,
            PasswordHash = PasswordHasher.Hash(demoPassword),
        });
        await db.SaveChangesAsync();
    }
}
