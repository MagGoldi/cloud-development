# Лабораторная работа №1 — «Кэширование»

**Вариант:** №29 — «Транспортное средство»  
**Балансировка:** Query Based  
**Брокер:** SQS  
**Хостинг S3:** Minio  
**Выполнил:** Нестеренко Андрей, 6512

## Что реализовано

- Генерация сущности «Транспортное средство» через Bogus.
- Кэширование результатов генерации через IDistributedCache (Redis) с TTL 10 минут.
- Структурное логирование запросов и результатов генерации.
- Оркестрация сервисов через .NET Aspire.
- REST endpoint: `GET /api/vehicle/{id}`.

# Лабораторная работа №2 — «Балансировка нагрузки»

## Описание

API-шлюз на основе Ocelot с кастомным алгоритмом балансировки нагрузки Query Based. Клиентские запросы распределяются по репликам сервиса генерации на основе параметра `id` из строки запроса.

## Что реализовано

### Репликация сервиса генерации
- Aspire AppHost запускает 3 реплики `ProjectApp.Api` на портах 5180, 5181, 5182
- Гейтвей ожидает готовности всех реплик перед стартом (`.WaitFor()`)

### API Gateway (Ocelot)
- Проект `ProjectApp.Gateway` — единая точка входа для клиента
- Маршрутизация настроена через `ocelot.json`
- CORS-политика вынесена в конфигурацию (`AllowedOrigins`)

### Кастомный балансировщик — `QueryBasedLoadBalancer`
- Реализует интерфейс `ILoadBalancer` из Ocelot
- Алгоритм: `index = id % N`, где `N` — число реплик
- При отсутствии параметра `id` запрос направляется на первую реплику

## Характеристики генерируемого транспортного средства

1. Идентификатор в системе — `int`
2. VIN-номер — `string`
3. Производитель — `string`
4. Модель — `string`
5. Год выпуска — `int`
6. Тип корпуса — `string`
7. Тип топлива — `string`
8. Цвет корпуса — `string`
9. Пробег — `double`
10. Дата последнего техобслуживания — `DateOnly`

## Правила генерации

- VIN-номер: берётся из раздела Vehicle (Bogus).
- Производитель: случайно выбирается из фиксированного списка популярных марок.
- Модель: выбирается из набора моделей, соответствующих производителю.
- Год выпуска: от 1984 до текущего года включительно.
- Тип корпуса: берётся из раздела Vehicle (Bogus).
- Тип топлива: берётся из раздела Vehicle (Bogus); выбирается из списка (Бензин, Дизель, Электро, Гибрид, Газ).
- Цвет корпуса: берётся из раздела Commerce (Bogus).
- Пробег: от 0 до 500 000 км, не может быть меньше нуля.
- Дата последнего техобслуживания: не ранее 1 января года выпуска и не позже сегодняшней даты.

# Лабораторная работа №3 — «Интеграционное тестирование»

## Описание

Добавлен файловый сервис, объектное хранилище MinIO и очередь сообщений SQS (ElasticMQ). Написаны интеграционные тесты для проверки совместной работы всех компонентов.

## Что реализовано

### Объектное хранилище (MinIO)
- В оркестрацию Aspire добавлен контейнер MinIO (`minio/minio`)
- MinIO доступен на порту 9000 (API) и 9001 (Console)
- Учётные данные по умолчанию: `minioadmin` / `minioadmin`
- Бакет `vehicles` создаётся автоматически при старте файлового сервиса

### Очередь сообщений (SQS → ElasticMQ)
- В оркестрацию добавлен контейнер ElasticMQ (`softwaremill/elasticmq-native`) — легковесная SQS-совместимая очередь
- ElasticMQ доступен на порту 9324
- Очередь `vehicle-queue` создаётся автоматически

### Файловый сервис (`ProjectApp.FileService`)
- `SqsConsumerService` — фоновый сервис (`BackgroundService`), который опрашивает очередь SQS
- `MinioStorageService` — сохраняет десериализованные данные транспортного средства в MinIO как JSON-файлы
- Файлы хранятся в бакете `vehicles` с именами вида `vehicle-{id}.json`

### Отправка данных через SQS
- `SqsPublisher` в проекте `ProjectApp.Api` отправляет JSON-сериализованные данные транспортного средства в очередь после генерации
- Ошибки отправки в SQS не блокируют ответ клиенту

### Интеграционные тесты (`ProjectApp.Tests`)
- Фикстура `ServiceFixture` поднимает контейнеры Redis, MinIO и ElasticMQ через Testcontainers
- `WebApplicationFactory<Program>` запускает API с подменёнными подключениями к тестовым контейнерам
- Между тестами выполняется сброс состояния (flush Redis, purge SQS)

### Список тестов

| Тест | Что проверяет |
|------|---------------|
| `GetVehicle_ReturnsValidData` | Генерация транспортного средства возвращает корректные поля |
| `GetVehicle_SecondRequest_ReturnsCachedData` | Повторный запрос с тем же ID возвращает закэшированные данные |
| `GetVehicle_PublishesMessageToSqs` | После генерации сообщение попадает в очередь SQS |
| `GetVehicle_WithInvalidId_ReturnsBadRequest` | Запрос с невалидным ID (≤ 0) возвращает 400 Bad Request |
| `MinioStorage_SavesAndRetrievesFile` | Запись и чтение JSON-файла из MinIO |
| `Redis_CachesVehicleData` | Данные сохраняются в Redis после генерации |

# Лабораторная работа №4 — «Переход на облачную инфраструктуру»

## Описание

Все сервисы перенесены в Yandex Cloud. Локальная инфраструктура (Aspire, Docker-контейнеры) заменена облачными managed-сервисами. Клиентское приложение размещено в Object Storage, сервисы генерации и обработки файлов развёрнуты как Cloud Functions, маршрутизация настроена через Serverless API Gateway, очередь сообщений перенесена в Yandex Message Queue, а объектное хранилище файлов — в отдельный бакет Object Storage.

## Что реализовано

### Клиент (`Client.Wasm`) → Object Storage
- Blazor WebAssembly собирается в Release с конфигурацией `appsettings.Production.json`
- Статические файлы загружаются в бакет `vehicles-client`
- Включён режим статического сайта с `index.html` по умолчанию
- `BaseAddress` клиента указывает на URL API Gateway

### Сервис генерации (`ProjectApp.Api.Function`) → Cloud Function
- Проект `ProjectApp.Api.Function` — самодостаточная Cloud Function (без Aspire, без Redis)
- Точка входа: `ApiFunction.Handler`, метод `FunctionHandler(string input) : string`
- Runtime: `dotnet8`, память: 256 МБ, таймаут: 30 с
- Деплой через архив с исходным кодом (`.cs` + `.csproj`); YC компилирует на своей стороне
- Получает HTTP-запрос от API Gateway, парсит `id` из поля `pathParams`
- Генерирует транспортное средство случайным образом, публикует JSON в очередь через `AWSSDK.SQS`
- Возвращает JSON-ответ с CORS-заголовками в формате `{ statusCode, headers, body }`
- Конфигурация через переменные окружения: `SQS_QUEUE_URL`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`

### API Gateway → Serverless API Gateway
- OpenAPI 3.0 спецификация (`cloud/api-gateway.yaml`)
- Маршрут `GET /api/vehicle/{id}` интегрирован с Cloud Function через расширение `x-yc-apigateway-integration: type: cloud_functions`
- CORS настроен на уровне Gateway (`x-yc-apigateway: cors`)
- Балансировка нагрузки при масштабировании осуществляется платформой автоматически

### Брокер сообщений → Yandex Message Queue
- Очередь `vehicle-queue` создаётся через boto3 (SQS-совместимый API)
- Endpoint: `https://message-queue.api.cloud.yandex.net`, регион `ru-central1`
- Триггер `vehicle-mq-trigger` связывает очередь с Cloud Function файлового сервиса (batch size 10, окно 10 с)

### Файловый сервис (`ProjectApp.FileService.Function`) → Cloud Function
- Проект `ProjectApp.FileService.Function` — Cloud Function с триггером на Message Queue
- Точка входа: `FileServiceFunction.Handler`, метод `FunctionHandler(string input) : string`
- Runtime: `dotnet8`, память: 256 МБ, таймаут: 60 с
- Деплой через архив с исходным кодом; зависимость `AWSSDK.S3` устанавливается YC при сборке
- Получает пакет сообщений из очереди, декодирует тело (base64 или plain) и сохраняет в Object Storage
- Конфигурация через переменные окружения: `S3_ENDPOINT`, `S3_BUCKET`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`

### Объектное хранилище → Object Storage
- Бакет `vehicle-data-store` — хранит JSON-файлы транспортных средств (`vehicle-{id}.json`)
- Бакет создаётся через boto3 от имени сервисного аккаунта (владелец — `vehicle-sa`)
- Доступ через `AWSSDK.S3` (YC Object Storage совместим с S3 API), endpoint: `https://storage.yandexcloud.net`


## Ресурсы

| Ресурс | Значение |
|--------|----------|
| API Gateway URL | `https://d5djaicgufnt5rijrt0u.p8361f8z.apigw.yandexcloud.net` |
| Клиент | `http://vehicles-client.website.yandexcloud.net` |
| Очередь | `vehicle-queue` |
| Бакет файлов | `vehicle-data-store` |
| Бакет клиента | `vehicles-client` |