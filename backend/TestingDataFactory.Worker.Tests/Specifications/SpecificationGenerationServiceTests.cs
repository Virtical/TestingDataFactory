using Microsoft.Extensions.Options;
using NUnit.Framework;
using TestingDataFactory.Contracts;
using TestingDataFactory.Worker.Configuration;
using TestingDataFactory.Worker.Specifications;
using TestingDataFactory.Worker.Specifications.Extraction;
using TestingDataFactory.Worker.Specifications.Model;
using TestingDataFactory.Worker.Specifications.Validation;

namespace TestingDataFactory.Worker.Tests.Specifications;

[TestFixture]
public sealed class SpecificationGenerationServiceTests
{
    private const string ValidYaml = "spec_version: '1.0'\ntables: { users: { fields: { id: { type: uuid, primary: true } } } }";
    private const string InvalidYaml = "spec_version: '1.0'\ntables: { users: { fields: { id: { type: uuid, typo: true } } } }";

    [Test]
    public async Task RepairsInvalidYamlUsingCoreDiagnostics()
    {
        var model = new ModelStub(InvalidYaml, ValidYaml);
        var result = await CreateService(model).GenerateAsync("Create users.", CancellationToken.None);

        Assert.That(result, Is.EqualTo(new SpecificationGenerationResult.Succeeded(ValidYaml)));
        Assert.That(model.Requests, Has.Count.EqualTo(2));
        var correction = model.Requests[1].Correction!;
        Assert.That(model.Requests[1].Prompt, Is.EqualTo("Create users."));
        Assert.That(correction.Yaml, Is.EqualTo(InvalidYaml));
        Assert.That(correction.Diagnostics[0].Code, Is.EqualTo("UNSUPPORTED_PARAMETER"));
        Assert.That(correction.Diagnostics[0].AllowedKeys, Does.Contain("type"));
    }

    [TestCase(0, 1)]
    [TestCase(2, 3)]
    public async Task StopsAfterConfiguredCorrections(int corrections, int expectedCalls)
    {
        var model = new ModelStub(InvalidYaml, InvalidYaml, InvalidYaml);
        var result = await CreateService(model, corrections).GenerateAsync("request", CancellationToken.None);

        Assert.That(result, Is.TypeOf<SpecificationGenerationResult.Failed>());
        Assert.That(((SpecificationGenerationResult.Failed)result).Code,
            Is.EqualTo(SpecificationGenerationErrorCodes.InvalidSpecification));
        Assert.That(model.Requests, Has.Count.EqualTo(expectedCalls));
    }

    [Test]
    public async Task ReturnsValidYamlWithoutCorrection()
    {
        var model = new ModelStub(ValidYaml);
        var result = await CreateService(model).GenerateAsync("request", CancellationToken.None);
        Assert.That(result, Is.EqualTo(new SpecificationGenerationResult.Succeeded(ValidYaml)));
        Assert.That(model.Requests, Has.Count.EqualTo(1));
        Assert.That(model.Requests[0].Correction, Is.Null);
    }

    [Test]
    public async Task EmptyResponseFailsWithoutCorrection()
    {
        var model = new ModelStub("");
        var result = await CreateService(model).GenerateAsync("request", CancellationToken.None);
        Assert.That(((SpecificationGenerationResult.Failed)result).Code,
            Is.EqualTo(SpecificationGenerationErrorCodes.EmptyModelResponse));
        Assert.That(model.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public void CallerCancellationStopsBeforeRequest()
    {
        var model = new ModelStub(ValidYaml);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateService(model).GenerateAsync("request", cancellation.Token));
        Assert.That(model.Requests, Is.Empty);
    }

    [TestCase("spec_version: '1.0'\ntables: { t: { fields: { id: { ref: absent.id } } } }", "UNKNOWN_REFERENCE_TABLE")]
    [TestCase("spec_version: '1.0'\ntables: { t: { fields: { id: { type: integer } } } }\ngeneration: { rows: { missing: 1 } }", "UNKNOWN_ROWS_TABLE")]
    public async Task ValidatorChecksSemanticsAndGenerationOptions(string yaml, string expectedCode)
    {
        var diagnostics = await new CoreSpecificationValidator().ValidateAsync(yaml, CancellationToken.None);
        Assert.That(diagnostics.Select(d => d.Code), Does.Contain(expectedCode));
    }

    [Test]
    public void TransientFailurePropagatesToBrokerRetry()
    {
        var model = new ModelStub { Failure = new TransientModelException() };
        Assert.ThrowsAsync<TransientModelException>(() =>
            CreateService(model).GenerateAsync("request", CancellationToken.None));
    }

    [Test]
    public async Task PermanentFailureReturnsSafeError()
    {
        var model = new ModelStub { Failure = new PermanentModelException(PermanentModelFailure.Rejected) };
        var result = await CreateService(model).GenerateAsync("request", CancellationToken.None);
        Assert.That(((SpecificationGenerationResult.Failed)result).Code,
            Is.EqualTo(SpecificationGenerationErrorCodes.ModelRejected));
    }

    [Test]
    public void ModelRequestTimeoutBecomesTransientFailure()
    {
        var model = new ModelStub { WaitForCancellation = true };
        var service = new SpecificationGenerationService(
            model, new MarkdownSpecificationResponseExtractor(), new CoreSpecificationValidator(),
            Options.Create(new ModelClientOptions { RequestTimeout = TimeSpan.FromMilliseconds(20) }));

        Assert.ThrowsAsync<TransientModelException>(() =>
            service.GenerateAsync("request", CancellationToken.None));
        Assert.That(model.Requests, Has.Count.EqualTo(1));
    }

    private static SpecificationGenerationService CreateService(ModelStub model, int corrections = 2) =>
        new(model, new MarkdownSpecificationResponseExtractor(), new CoreSpecificationValidator(),
            Options.Create(new ModelClientOptions { MaxCorrectionAttempts = corrections }));

    private sealed class ModelStub(params string[] responses) : ISpecificationModelClient
    {
        public List<(string Prompt, SpecificationCorrection? Correction)> Requests { get; } = [];
        public Exception? Failure { get; init; }
        public bool WaitForCancellation { get; init; }

        public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken,
            SpecificationCorrection? correction = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add((prompt, correction));
            if (WaitForCancellation)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if (Failure is not null)
                throw Failure;
            return responses[Requests.Count - 1];
        }
    }
}
