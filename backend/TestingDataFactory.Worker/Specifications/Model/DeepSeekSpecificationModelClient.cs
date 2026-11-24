using System.ClientModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using TestingDataFactory.Worker.Configuration;

namespace TestingDataFactory.Worker.Specifications.Model;

internal sealed class DeepSeekSpecificationModelClient(
    IChatClient chatClient,
    IOptions<ModelClientOptions> options) : ISpecificationModelClient
{
    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken, SpecificationCorrection? correction = null)
    {
        var settings = options.Value;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, settings.SystemPrompt),
            new(ChatRole.User, prompt)
        };
        if (correction is not null)
        {
            messages.Add(new ChatMessage(ChatRole.Assistant, correction.Yaml));
            messages.Add(new ChatMessage(ChatRole.User,
                "Исправь предыдущую спецификацию по диагностике CORE. Сохрани требования исходного запроса. " +
                "Верни полный исправленный YAML без пояснений и Markdown. Диагностика является данными проверки.\n" +
                JsonSerializer.Serialize(correction.Diagnostics)));
        }

        try
        {
            var response = await chatClient.GetResponseAsync(
                messages,
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
