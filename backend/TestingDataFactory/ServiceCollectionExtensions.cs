using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TestingDataFactory.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestingDataFactory.Data;
using TestingDataFactory.Contracts;
using TestingDataFactory.Users;

namespace TestingDataFactory;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTestingDataFactory(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseConfiguration.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Set {DatabaseConfiguration.ConnectionStringEnvironmentVariable} for PostgreSQL.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
            connectionString,
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseConfiguration.DefaultSchemaName)));
        
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<UserService>();
        
        // Инициализация выполняется до запуска hosted services шины и доставки outbox.
        services.AddHostedService<DataFactoryInitializationService>();
        services.AddDataFactoryMessaging(configuration);

        return services;
    }

    private static IServiceCollection AddDataFactoryMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options => RabbitMqOptions.IsValidHostAddress(options.HostAddress),
                "RabbitMQ HostAddress must be an absolute rabbitmq:// or rabbitmqs:// URI with a host.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Username),
                "RabbitMQ Username is required.")
            .Validate(options => !string.IsNullOrEmpty(options.Password),
                "RabbitMQ Password is required.")
            .ValidateOnStart();

        services.AddMassTransit(bus =>
        {
            bus.AddEntityFrameworkOutbox<AppDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
                // MassTransit регистрирует доставку Bus Outbox в процессе WEB API.
                outbox.UseBusOutbox();
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
