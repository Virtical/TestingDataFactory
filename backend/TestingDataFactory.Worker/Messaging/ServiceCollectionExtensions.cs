using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TestingDataFactory.Worker.Configuration;
using TestingDataFactory.Contracts;
using TestingDataFactory.Worker.Data;
using TestingDataFactory.Worker.Specifications;
using TestingDataFactory.Worker.Specifications.Extraction;
using TestingDataFactory.Worker.Specifications.Model;
using TestingDataFactory.Worker.Specifications.Validation;

namespace TestingDataFactory.Worker.Messaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSpecificationWorkerMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseConfiguration.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Set {DatabaseConfiguration.ConnectionStringEnvironmentVariable} for PostgreSQL.");

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options => RabbitMqOptions.IsValidHostAddress(options.HostAddress),
                "RabbitMQ HostAddress must be an absolute rabbitmq:// or rabbitmqs:// URI with a host.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Username),
                "RabbitMQ Username is required.")
            .Validate(options => !string.IsNullOrEmpty(options.Password),
                "RabbitMQ Password is required.")
            .ValidateOnStart();

        services.AddDbContext<WorkerDbContext>(options => options.UseNpgsql(connectionString,
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseConfiguration.DefaultSchemaName)));

        services.AddScoped<ISpecificationGenerationService, SpecificationGenerationService>();
        services.TryAddScoped<ISpecificationModelClient, UnconfiguredSpecificationModelClient>();
        services.TryAddSingleton<ISpecificationResponseExtractor, MarkdownSpecificationResponseExtractor>();
        services.TryAddSingleton<ISpecificationValidator, NoOpSpecificationValidator>();

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<GenerateSpecificationConsumer>()
                .Endpoint(endpoint => endpoint.Name = MessagingTopology.GenerateSpecificationQueue);
            bus.AddEntityFrameworkOutbox<WorkerDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.DuplicateDetectionWindow = TimeSpan.FromHours(24);
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
            });
            bus.AddConfigureEndpointsCallback((context, name, endpoint) =>
            {
                if (name == MessagingTopology.GenerateSpecificationQueue)
                    endpoint.UseMessageRetry(SpecificationGenerationRetry.Configure);

                endpoint.UseEntityFrameworkOutbox<WorkerDbContext>(context);
            });

            bus.UsingRabbitMq((context, transport) =>
            {
                var rabbitMq = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                transport.Host(new Uri(rabbitMq.HostAddress), host =>
                {
                    host.Username(rabbitMq.Username);
                    host.Password(rabbitMq.Password);
                });
                transport.Message<JobStarted>(
                    message => message.SetEntityName(MessagingTopology.JobStartedExchange));
                transport.Message<SpecificationGenerated>(
                    message => message.SetEntityName(MessagingTopology.SpecificationGeneratedExchange));
                transport.Message<JobFailed>(
                    message => message.SetEntityName(MessagingTopology.JobFailedExchange));
                transport.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
