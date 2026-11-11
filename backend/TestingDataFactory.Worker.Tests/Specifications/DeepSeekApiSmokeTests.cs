using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using TestingDataFactory.Worker.Configuration;
using TestingDataFactory.Worker.Specifications;
using TestingDataFactory.Worker.Specifications.Extraction;
using TestingDataFactory.Worker.Specifications.Model;
using TestingDataFactory.Worker.Specifications.Validation;

namespace TestingDataFactory.Worker.Tests.Specifications;

[TestFixture]
public sealed class DeepSeekApiSmokeTests
{
    private ModelClientOptions modelOptions = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<ModelClientOptions>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        modelOptions = configuration.GetSection(ModelClientOptions.SectionName).Get<ModelClientOptions>()
            ?? new ModelClientOptions();
    }

    [Test]
    [Explicit("Sends a live request to DeepSeek and prints the generated YAML.")]
    [Category("DeepSeekLive")]
    public async Task GeneratesAnEcommerceSpecification()
    {
        Assert.That(modelOptions.ApiKey, Is.Not.Null.And.Not.Empty,
            "ModelClient:ApiKey was not found in User Secrets.");

        const string prompt = """
            Создай реалистичную YAML-спецификацию синтетических тестовых данных для российского интернет-магазина.

            Опиши связанные таблицы:
            - customers — покупатели: идентификатор, уникальный email, ФИО, дата регистрации и признак активности;
            - products — товары: идентификатор, уникальный артикул (SKU), название, категория, цена и признак доступности;
            - orders — заказы: идентификатор, ссылка на покупателя, статус, дата и время создания, дата доставки и итоговая сумма;
            - order_items — позиции заказа: идентификатор, ссылка на заказ, ссылка на товар, количество и цена за единицу.

            Требования:
            - укажи spec_version '1.0' и locale ru_RU;
            - размести описания таблиц в tables, а поля — в fields;
            - используй UUID для первичных ключей;
            - задавай связи через ref в формате table.field;
            - задай для каждого заказа от одной до пяти позиций с помощью per_parent: { min: 1, max: 5 } у ссылки позиции на заказ;
            - объяви статусы заказа как пользовательский enum в types и используй реалистичные статусы российского интернет-магазина;
            - подбери подходящие типы, реалистичные ограничения, длины, уникальность и nullable-поля;
            - сделай спецификацию самодостаточной и внутренне согласованной;
            - верни только YAML без Markdown-ограждений и пояснений.
            """;

        var modelClient = new DeepSeekSpecificationModelClient(
            CreateChatClient(),
            Options.Create(modelOptions));
        var generationService = new SpecificationGenerationService(
            modelClient,
            new MarkdownSpecificationResponseExtractor(),
            new NoOpSpecificationValidator(),
            Options.Create(modelOptions));

        using var timeout = new CancellationTokenSource(modelOptions.RequestTimeout);
        var result = await generationService.GenerateAsync(prompt, timeout.Token);

        Assert.That(result, Is.TypeOf<SpecificationGenerationResult.Succeeded>());
        var generated = (SpecificationGenerationResult.Succeeded)result;
        Assert.That(generated.Yaml, Is.Not.Empty);
        TestContext.Progress.WriteLine(generated.Yaml);
    }

    private IChatClient CreateChatClient()
    {
        var chatClient = new OpenAI.Chat.ChatClient(
            modelOptions.Model,
            new System.ClientModel.ApiKeyCredential(modelOptions.ApiKey),
            new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(modelOptions.BaseUrl),
                RetryPolicy = new System.ClientModel.Primitives.ClientRetryPolicy(0)
            });

        return chatClient.AsIChatClient();
    }
}
