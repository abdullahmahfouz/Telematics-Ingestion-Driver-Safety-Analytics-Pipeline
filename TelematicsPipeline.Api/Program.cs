using Microsoft.EntityFrameworkCore;
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
