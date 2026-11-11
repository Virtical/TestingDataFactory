using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using TestingDataFactory.Worker.Configuration;
using TestingDataFactory.Worker.Specifications.Model;

namespace TestingDataFactory.Worker.Tests.Specifications;

[TestFixture]
public sealed class DeepSeekSpecificationModelClientTests
{
    [Test]
    public async Task GenerateAsync_SendsSystemAndUserMessagesAndReturnsResponseText()
    {
        var chatClient = new FakeChatClient();
        var settings = new ModelClientOptions
        {
            SystemPrompt = "Return YAML only.",
            MaxOutputTokens = 1234
        };
        var sut = new DeepSeekSpecificationModelClient(chatClient, Options.Create(settings));

        var result = await sut.GenerateAsync("Create a customer table.", CancellationToken.None);

        Assert.That(result, Is.EqualTo("tables: {}"));
        Assert.That(chatClient.Messages, Has.Count.EqualTo(2));
        Assert.That(chatClient.Messages[0].Role, Is.EqualTo(ChatRole.System));
        Assert.That(chatClient.Messages[0].Text, Is.EqualTo("Return YAML only."));
        Assert.That(chatClient.Messages[1].Role, Is.EqualTo(ChatRole.User));
        Assert.That(chatClient.Messages[1].Text, Is.EqualTo("Create a customer table."));
        Assert.That(chatClient.Options!.MaxOutputTokens, Is.EqualTo(1234));
        Assert.That(chatClient.Options.Reasoning!.Effort, Is.EqualTo(ReasoningEffort.None));
    }

    [Test]
    public void GenerateAsync_MapsNetworkFailureToTransientModelException()
    {
        var chatClient = new FakeChatClient { ExceptionToThrow = new HttpRequestException("sensitive provider detail") };
        var sut = new DeepSeekSpecificationModelClient(chatClient, Options.Create(new ModelClientOptions()));

        var exception = Assert.ThrowsAsync<TransientModelException>(
            () => sut.GenerateAsync("prompt", CancellationToken.None));

        Assert.That(exception!.Message, Does.Not.Contain("sensitive provider detail"));
    }

    private sealed class FakeChatClient : IChatClient
    {
        public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];
        public ChatOptions? Options { get; private set; }
        public Exception? ExceptionToThrow { get; init; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages = messages.ToArray();
            Options = options;

            if (ExceptionToThrow is not null)
                return Task.FromException<ChatResponse>(ExceptionToThrow);

            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "tables: {}")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
