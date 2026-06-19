using Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();
host.Run();