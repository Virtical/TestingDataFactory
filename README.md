# DataForge

DataForge генерирует YAML-спецификации синтетических тестовых данных по текстовому описанию. Фронтенд работает на React и Vite, API и воркер — на .NET. API ставит задачи в очередь RabbitMQ и хранит их статусы и результаты в PostgreSQL.

## Что понадобится

- .NET 10 SDK.
- Node.js 20.19 или новее в ветке 20.x либо Node.js 22.12 и выше. Требуется npm 11 или новее.
- Docker Desktop с Docker Compose.
- JetBrains Rider для запуска через IDE.
- Ключ API DeepSeek для генерации спецификаций.

## Настройка ключа DeepSeek

Один раз сохраните ключ в .NET User Secrets воркера. Выполните команды в PowerShell из корня репозитория и вставьте ключ по запросу.

```powershell
$deepSeekKey = Read-Host "Ключ API DeepSeek" -AsSecureString
dotnet user-secrets set "ModelClient:ApiKey" ([System.Net.NetworkCredential]::new('', $deepSeekKey).Password) --project backend/TestingDataFactory.Worker
```

## Запуск через Rider

1. Запустите Docker Desktop. Из корня репозитория поднимите PostgreSQL и RabbitMQ. Команда дождётся готовности обоих сервисов. [Конфигурация Compose](compose.yaml)

   ```powershell
   docker compose up -d --wait
   ```

2. Откройте [TestingDataFactory.slnx](TestingDataFactory.slnx) в Rider. Выберите конфигурацию **Frontend + Backend** и нажмите **Run**. Она запускает фронтенд, API и воркер. API и воркер автоматически применяют миграции в окружении Development. [Конфигурация Rider](<.run/Frontend + Backend.run.xml>)
3. Откройте приложение. Если порт по умолчанию занят, используйте адрес, который напечатал Vite. [Фронтенд на порту 5173](http://127.0.0.1:5173/)

## Демонстрация генерации

1. Зарегистрируйтесь или войдите в приложение.
2. На странице **«Новый проект»** введите описание и нажмите **«Создать черновик»**. Можно использовать этот пример.

   > Создай YAML-спецификацию для таблицы покупателей интернет-магазина. У каждого покупателя есть UUID как первичный ключ и дата регистрации.

3. Дождитесь появления блока **«Черновик спецификации»** под формой. Готовый YAML можно скопировать или скачать.

## Запуск из терминалов

Поднимите инфраструктуру командой `docker compose up -d --wait`. Затем откройте три терминала в корне репозитория и запустите по одному процессу в каждом.

**API**

```powershell
dotnet run --project backend/TestingDataFactory.Api --launch-profile http
```

**Воркер**

```powershell
dotnet run --project backend/TestingDataFactory.Worker --launch-profile TestingDataFactory.SpecificationWorker
```

**Фронтенд**

```powershell
cd frontend
npm install
npm run dev
```

## Локальные адреса

| Сервис | Адрес |
| --- | --- |
| Фронтенд | [127.0.0.1:5173](http://127.0.0.1:5173/) |
| API | [Проверка готовности](http://localhost:5141/api/health) · [Swagger](http://localhost:5141/swagger) |
| Панель RabbitMQ | [127.0.0.1:15672](http://127.0.0.1:15672/) · логин `dataforge`, пароль `dataforge-local` |
| PostgreSQL | `127.0.0.1:5432` · база `testing_data_factory` |

## Остановка

Остановите конфигурацию Rider или процессы в терминалах. Затем из корня репозитория остановите PostgreSQL и RabbitMQ.

```powershell
docker compose down
```

Данные сохраняются в томах Docker и будут доступны при следующем запуске.
