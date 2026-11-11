namespace TestingDataFactory.Worker.Configuration;

internal sealed class ModelClientOptions
{
    public const string SectionName = "ModelClient";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.deepseek.com";
    public string Model { get; set; } = "deepseek-flash";
    public string SystemPrompt { get; set; } = """
        Ты создаёшь YAML-спецификации синтетических данных для DataForge. Следуй формату ниже и требованиям пользователя.

        Верни ровно один YAML-документ без Markdown-ограждений и пояснений. Не добавляй поля и конструкции, которых нет в описании формата. Если требование пользователя конфликтует с форматом, соблюдай формат и выбери ближайшее поддерживаемое решение.

        Структура документа:
        - spec_version: обязательная строка; используй '1.0'.
        - locale: необязательная локаль по умолчанию; если не задана, это ru_RU.
        - types: необязательные пользовательские типы.
        - tables: обязательный набор таблиц. У каждой таблицы есть fields и при необходимости rules.
        - generation: необязательные параметры seed, rows, mode и output. Этот блок описывает генерацию, его не читает валидатор.

        Типы данных:
        - integer: min, max; decimal: min, max, scale.
        - string: min_length, max_length; regex: pattern.
        - boolean: true_rate; date, datetime, time: min, max в ISO 8601; uuid без дополнительных параметров.
        - Собственные типы объявляй в types. Enum объявляется только в types, но не как базовый тип: список значений или объект весов; для справочников допустимы dictionary и многоколоночные rows/columns.
        - Для пользовательского типа enum с несколькими колонками указывай column в поле. Для справочника допустим where с CEL-условием по колонкам.
        - Семантические генераторы задаются через generator, например person.full_name, internet.email, phone, address.city, finance.card_number.
        - object и array описаны как черновые возможности: используй их только если запрос явно требует вложенные структуры; в v1 не используй oneOf и массивы с разными формами элементов.

        Поля таблиц:
        Каждое поле записывай как имя: { параметры }. У поля должен быть ровно один источник значений: type, generator, ref или derived.
        - ref указывает внешний ключ в формате table.field; per_parent: { min, max } задаёт число дочерних строк на одну родительскую строку и допустим только для ref.
        - derived содержит CEL-выражение, вычисляемое из других полей этой же строки.
        - primary: true означает unique и not null. Также доступны unique: true, null_rate от 0 до 1, min/max, min_length/max_length, distribution, locale и pii: true.
        - Для distribution используй uniform (по умолчанию), lognormal или вложенную форму, например { normal: { mean: 35, std: 12 } }.
        - Учитывай локаль на уровне поля, затем типа, затем всей спецификации. Локаль влияет на генерируемые имена, адреса и телефоны, но не меняет формат дат и чисел.

        CEL используется в derived, rules и where. Поддерживаемые операции: сравнения ==, !=, <, <=, >, >=; логические &&, ||, ! и условие a ? b : c; арифметика +, -, *, /, %; x in [...]; сравнение с null; функции size(), contains(), startsWith(), endsWith(), matches(). Не используй другие функции, обращения к другим таблицам или текущее время. При делении целых чисел избегай неожиданного целочисленного результата.

        Правила таблицы задаются списком rules. У правила обязательный expr (условие CEL) и необязательные when, message и severity (error по умолчанию; допустимо warning). Правило должно описывать согласованное условие между полями этой таблицы.

        generation, если включён:
        - seed задаёт воспроизводимость.
        - rows содержит количества строк корневых таблиц; дочерние количества выводятся из per_parent.
        - mode: valid (по умолчанию), edge_cases или anomalies.
        - output должен содержать ровно один формат: csv, json или sql. Для csv доступен delimiter; для sql — dialect и style (script или insert).

        Общие ограничения: даты — ISO 8601, десятичный разделитель — точка, boolean — true/false. Связанные таблицы должны ссылаться на существующие поля. Циклические зависимости между таблицами не допускаются.
        """;
    public int MaxOutputTokens { get; set; } = 8192;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(3);
}
