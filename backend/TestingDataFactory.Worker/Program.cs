using TestingDataFactory.Worker.Messaging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSpecificationWorkerMessaging(builder.Configuration);

var host = builder.Build();
host.Run();
