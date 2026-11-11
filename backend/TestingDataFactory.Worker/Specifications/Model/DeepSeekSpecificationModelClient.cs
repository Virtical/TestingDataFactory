using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using TestingDataFactory.Worker.Configuration;

namespace TestingDataFactory.Worker.Specifications.Model;

internal sealed class DeepSeekSpecificationModelClient(
    IChatClient chatClient,
    IOptions<ModelClientOptions> options) : ISpecificationModelClient
{
    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        try
        {
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, settings.SystemPrompt),
                    new ChatMessage(ChatRole.User, prompt)
                ],
                new ChatOptions
                {
                    MaxOutputTokens = settings.MaxOutputTokens,
                    Reasoning = new ReasoningOptions { Effort = ReasoningEffort.None }
                },
                cancellationToken);

            return response.Text ?? string.Empty;
        }
        catch (ClientResultException exception) when (IsTransientStatus(exception.Status))
        {
            throw new TransientModelException();
        }
        catch (ClientResultException)
        {
            throw new PermanentModelException(PermanentModelFailure.Rejected);
        }
        catch (HttpRequestException)
        {
            throw new TransientModelException();
        }
        catch (TimeoutException)
        {
            throw new TransientModelException();
        }
    }

    private static bool IsTransientStatus(int status) =>
        status is 408 or 429 || status >= 500;
}
