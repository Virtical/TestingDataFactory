using TestingDataFactory.Worker.Messaging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(45));
builder.Services.AddSpecificationWorkerMessaging(builder.Configuration);

var host = builder.Build();
await host.RunAsync();
