using Worker;
using Worker.Execution;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IExperimentExecutor, SimulatedExperimentExecutor>();
builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();
host.Run();