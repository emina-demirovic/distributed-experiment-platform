using Coordinator.Services;
using Coordinator.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSingleton<WorkerRegistry>();
builder.Services.AddSingleton<ExperimentRegistry>();
builder.Services.AddHostedService<ExperimentSchedulerService>();
builder.Services.AddHostedService<ExperimentRecoveryService>();

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var databasePath = Path.Combine(
    builder.Environment.ContentRootPath,
    "Data",
    "coordinator.db");

builder.Services.AddDbContextFactory<CoordinatorDbContext>(
    options => options.UseSqlite(
        $"Data Source={databasePath}"));
        
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
