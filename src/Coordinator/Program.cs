using Coordinator.Services;

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

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
