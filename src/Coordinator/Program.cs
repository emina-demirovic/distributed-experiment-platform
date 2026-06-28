using Coordinator.Services;
using Coordinator.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var databasePath = Path.Combine(
    builder.Environment.ContentRootPath,
    "Data",
    "coordinator.db");

builder.Services.AddDbContextFactory<CoordinatorDbContext>(
    options => options.UseSqlite(
        $"Data Source={databasePath}",
        sqliteOptions => sqliteOptions.MigrationsAssembly(
            typeof(CoordinatorDbContext).Assembly.FullName)));

Directory.CreateDirectory(
    Path.GetDirectoryName(databasePath)!);
        
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSingleton<WorkerRegistry>();
builder.Services.AddSingleton<ExperimentRegistry>();
builder.Services.AddHostedService<ExperimentSchedulerService>();
builder.Services.AddHostedService<ExperimentRecoveryService>();

builder.Services.AddOpenApi();

var app = builder.Build();
        
using (var scope = app.Services.CreateScope())
{
    var dbContextFactory = scope.ServiceProvider
        .GetRequiredService<
            IDbContextFactory<CoordinatorDbContext>>();

    await using var dbContext =
        await dbContextFactory.CreateDbContextAsync();

    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
