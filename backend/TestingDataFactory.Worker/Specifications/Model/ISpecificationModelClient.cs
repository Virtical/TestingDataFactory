namespace TestingDataFactory.Worker.Specifications.Model;

public interface ISpecificationModelClient
{
    /// <summary>
    /// Запрашивает спецификацию у модели. Адаптер должен соблюдать отмену и исключать
    /// prompt, ответ провайдера и секреты из логов и текста пробрасываемых исключений.
    /// </summary>
    /// <exception cref="TransientModelException">Временный сетевой сбой, тайм-аут модели, HTTP 429 или временный отказ провайдера.</exception>
    /// <exception cref="PermanentModelException">Запрос окончательно отклонён или модель не настроена.</exception>
    /// <exception cref="OperationCanceledException">Вызов отменён переданным токеном.</exception>
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken);
}
