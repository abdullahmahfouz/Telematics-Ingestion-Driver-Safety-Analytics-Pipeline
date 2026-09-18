using Microsoft.EntityFrameworkCore;
using TelematicsPipeline.Api.Persistence;
using TelematicsPipeline.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<TelematicsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TelematicsDb")));

builder.Services.AddScoped<TelematicsQueryService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
