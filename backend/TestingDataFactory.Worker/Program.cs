using Microsoft.EntityFrameworkCore;
using TestingDataFactory.Worker.Data;
using TestingDataFactory.Worker.Messaging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(45));
builder.Services.AddSpecificationWorkerMessaging(builder.Configuration);

var host = builder.Build();
if (builder.Environment.IsDevelopment())
{
    await using var scope = host.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
    await db.Database.MigrateAsync();
}

await host.RunAsync();
