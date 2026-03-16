# ITPlotter Backend

REST API для системы мониторинга печати, построенный на .NET 10 с чистой архитектурой.

## Архитектура

```
src/
├── ITPlotter.API/              # Контроллеры, конфигурация, Dockerfile
├── ITPlotter.Application/      # Сервисы, DTO, валидаторы (FluentValidation)
├── ITPlotter.Domain/           # Сущности, перечисления, интерфейсы
└── ITPlotter.Infrastructure/   # EF Core, MinIO, CUPS, PDF-обработка
```

## Стек технологий

- **.NET 10** / ASP.NET Core
- **Entity Framework Core** + PostgreSQL
- **MinIO** — S3-совместимое хранилище документов
- **CUPS** — управление принтерами и заданиями печати
- **JWT** — аутентификация (access + refresh токены)
- **FluentValidation** — валидация запросов
- **BCrypt** — хеширование паролей

## API эндпоинты

### Auth (`/api/auth`)

| Метод | Маршрут | Описание |
|-------|---------|----------|
| POST | `/api/auth/register` | Регистрация нового пользователя |
| POST | `/api/auth/login` | Авторизация, возвращает JWT + refresh токен |
| POST | `/api/auth/refresh` | Обновление access токена |
| GET | `/api/auth/profile` | Профиль текущего пользователя (требует авторизации) |

### Printers (`/api/printers`) — требует авторизации

| Метод | Маршрут | Описание |
|-------|---------|----------|
| GET | `/api/printers` | Список всех принтеров |
| GET | `/api/printers/{id}` | Получить принтер по ID |
| POST | `/api/printers` | Добавить принтер (регистрирует в CUPS) |
| PUT | `/api/printers/{id}` | Обновить данные принтера |
| DELETE | `/api/printers/{id}` | Удалить принтер (удаляет из CUPS) |
| POST | `/api/printers/{id}/sync-status` | Синхронизировать статус с CUPS |

### Documents (`/api/documents`) — требует авторизации

| Метод | Маршрут | Описание |
|-------|---------|----------|
| POST | `/api/documents` | Загрузить документ (PDF, DOC, DOCX, до 200 МБ) |
| GET | `/api/documents` | Список документов пользователя |
| GET | `/api/documents/{id}/download` | Скачать документ |
| DELETE | `/api/documents/{id}` | Удалить документ |

### Print Jobs (`/api/printjobs`) — требует авторизации

| Метод | Маршрут | Описание |
|-------|---------|----------|
| POST | `/api/printjobs` | Создать задание печати |
| GET | `/api/printjobs` | Список заданий пользователя |
| GET | `/api/printjobs/{id}` | Статус задания (синхронизирует с CUPS) |
| DELETE | `/api/printjobs/{id}` | Отменить задание |

### Optimization (`/api/optimization`) — требует авторизации

| Метод | Маршрут | Описание |
|-------|---------|----------|
| POST | `/api/optimization/{documentId}` | Оптимизировать PDF для плоттерной печати |

## Запуск

### Docker Compose (рекомендуется)

```bash
docker compose up -d
```

Сервисы:
- **API** — `http://localhost:5000`
- **PostgreSQL** — `localhost:5432`
- **MinIO Console** — `http://localhost:9001` (minioadmin / minioadmin)
- **CUPS** — `http://localhost:631`

### Локально

Требования: .NET 10 SDK, запущенные PostgreSQL, MinIO, CUPS.

```bash
cd src/ITPlotter.API
dotnet run
```

## Конфигурация

Настройки в `src/ITPlotter.API/appsettings.json`:

| Параметр | Описание | Значение по умолчанию |
|----------|----------|-----------------------|
| `ConnectionStrings:DefaultConnection` | Строка подключения к PostgreSQL | `Host=localhost;Port=5432;...` |
| `Jwt:Key` | Секретный ключ JWT (мин. 32 символа) | Сменить в production! |
| `Jwt:AccessTokenExpirationMinutes` | Время жизни access токена | 30 |
| `Jwt:RefreshTokenExpirationDays` | Время жизни refresh токена | 7 |
| `Minio:Endpoint` | Адрес MinIO | `localhost:9000` |
| `Cups:BaseUrl` | Адрес CUPS сервера | `http://localhost:631` |

## Модель данных

**Сущности:** User, RefreshToken, Document, Printer, PrintJob

**Перечисления:**
- `PrinterType` — Printer, Plotter
- `PrinterStatus` — Idle, Printing, PaperJam, OutOfPaper, OutOfToner, OutOfInk, Error, Offline
- `PrintJobStatus` — Pending, Processing, Printing, Completed, Failed, Cancelled
- `DocumentFormat` — Pdf, Doc, Docx
- `PaperFormat` — A4, A3, A2, A1, A0
