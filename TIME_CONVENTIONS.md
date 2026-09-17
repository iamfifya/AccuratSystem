# Конвенции времени v2 (ДЕЙСТВУЕТ)

## Статус
Действует с cutover-коммита `refactor(time): cutover на честное UTC-хранение`.
Конвенция v1 («настенное время филиала, помеченное как UTC») УСТРАНЕНА:
legacy-даныe dev-БД сдвинуты SQL-миграцией (см. TIME_MIGRATION_RUNBOOK.md).

## Правила

### 1. Хранилище и провод = абсолютные моменты
- PostgreSQL: колонки `timestamptz` содержат честный UTC-инстант.
- Провод API: ISO-8601 с `Z`/offset = абсолютный момент.
- Никаких «настенных» значений в БД и в JSON быть не должно.

### 2. Сервер
- Серверные таймстампы событий создаются ТОЛЬКО `DateTime.UtcNow`
  (заказы, смены, транзакции, ленты, Outbox).
- Бизнес-семантика (день, час, границы суток, диапазоны отчётов) извлекается
  ТОЛЬКО через `Accurat.WebAPI.Time.BusinessTime` с ЯВНОЙ зоной филиала:
  `InBranchZone`, `BusinessDay(zone)`, `BusinessHour(zone)`,
  `StoredRangeInclusive(..., zone)`, `ToWireDate(date, zone)`.
- Входящие `DateTime` из query/тел нормализуются через
  `BusinessTime.ToInstantUtc(dt)` перед сравнением с колонками.
- Запрещены: `DateTime.SpecifyKind`, `DateTime.Now` для записи/провода,
  raw `.Date` / `.Hour` для бизнес-логики.

### 3. Зона филиала
- `Branch.TimeZoneId` — IANA-идентификатор (пусто → `Europe/Moscow`).
- Разрешение зоны — только через `IBranchZoneResolver` (`BranchZoneResolver`).
- Мульти-зонность поддерживается по построению: все группировки идут в зоне филиала.

### 4. Клиенты (WPF net10 / MAUI / WPF net462)
- UI-модели и view-модели держат ЛОКАЛЬНОЕ настенное время (`DateTime`).
- ЕДИНСТВЕННАЯ точка конверсии — `Services/JsonTime.cs` каждого клиента:
  - `UtcLocalDateTimeConverter`: чтение UTC→Local, запись Local→UTC;
  - `JsonOpts.Default` — общие опции сериализации;
  - `HttpJsonExtensions`: `GetJsonAsync` / `PostJsonAsync` / `PutJsonAsync` /
    `PatchJsonAsync` / `ReadJsonAsync` — весь HTTP-JSON идёт через них;
  - приватный `SafeGetAsync` (404/пустое тело → default) тоже с `JsonOpts`.
- `JsonContent.Create(...)` всегда с `options: JsonOpts.Default`.
- Запрещены в клиентах: `TimeHelper` (УДАЛЁН), `SpecifyKind`,
  ручные `ToLocalTime()` / `ToUniversalTime()` в окнах и view-моделях.

### 5. Что НЕ сдвигается никогда
- `OutboxMessages.CreatedAtUtc / ProcessedAtUtc` — уже честный UTC.
- Серверные `Timestamp` в лентах, созданные после cutover, — честный UTC.

## Таблица точек конверсии
| Слой | Направление | Где |
|------|-------------|-----|
| Клиент → сервер | Local wall → UTC instant | `UtcLocalDateTimeConverter.Write` |
| Сервер → клиент | UTC instant → Local wall | `UtcLocalDateTimeConverter.Read` |
| Query-параметры дат | любой Kind → UTC instant | `BusinessTime.ToInstantUtc` |
| Отчётные границы суток | LocalDate зоны → UTC instant | `BusinessTime.StoredRangeInclusive` |
| DTO-даты на провод | LocalDate зоны → UTC instant | `BusinessTime.ToWireDate` |

## История
- **v1** (устарела): настенное время филиала хранилось и передавалось как UTC
  (`SpecifyKind`); клиенты вручную сдвигали через `TimeHelper.ToMsk/ToUtc`.
- **Cutover**: SQL-сдвиг legacy-значений по зонам филиалов + конвертеры на краю.
- **v2** (действует): данная конвенция.