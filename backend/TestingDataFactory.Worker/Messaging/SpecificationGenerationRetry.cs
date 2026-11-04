using System.Collections.Immutable;
using MassTransit;
using TestingDataFactory.Worker.Specifications.Model;

namespace TestingDataFactory.Worker.Messaging;

internal static class SpecificationGenerationRetry
{
    private static readonly ImmutableArray<TimeSpan> Delays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)];

    public static int RetryLimit => Delays.Length;

    public static void Configure(IRetryConfigurator retry)
    {
        retry.Handle<TransientModelException>();
        retry.Intervals(Delays.ToArray());
    }
}
