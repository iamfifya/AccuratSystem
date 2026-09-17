# Runbook: миграция данных времени v1 → v2

Назначение: перевод legacy-БД («настенное время как UTC») в честные инстанты.
Выполнен ОДИН РАЗ на dev-БД (псевдоданные). Хранится для боевой миграции.

⚠️ НЕ ЗАПУСКАТЬ ПОВТОРНО на уже мигрированной БД — сдвинет данные дважды.

## Пре-чеки
1. Бэкап БД обязателен.
2. Остановить запись (maintenance) или согласовать окно.
3. Проверить `Branch.TimeZoneId`: пусто → будет использован 'Europe/Moscow'.
   SELECT "Id", "Name", "TimeZoneId" FROM "Branches";

## Формула
(col AT TIME ZONE 'UTC') AT TIME ZONE <zone>
— трактует сохранённые поля как настенное время в зоне филиала
и превращает в честный timestamptz-инстант.

## Скрипт
UPDATE "Orders" o SET
  "Time" = ((o."Time" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow')),
  "FinishedAt" = CASE WHEN o."FinishedAt" IS NULL THEN NULL
    ELSE ((o."FinishedAt" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow')) END
FROM "Branches" b WHERE b."Id" = o."BranchId";

UPDATE "Transactions" t SET
  "DateTime" = ((t."DateTime" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow'))
FROM "Branches" b WHERE b."Id" = t."BranchId";

UPDATE "Shifts" s SET
  "Date" = ((s."Date" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow')),
  "StartTime" = CASE WHEN s."StartTime" IS NULL THEN NULL
    ELSE ((s."StartTime" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow')) END,
  "EndTime" = CASE WHEN s."EndTime" IS NULL THEN NULL
    ELSE ((s."EndTime" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow')) END
FROM "Branches" b WHERE b."Id" = s."BranchId";

UPDATE "Clients" c SET
  "RegistrationDate" = ((c."RegistrationDate" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow')),
  "LastVisitDate" = CASE WHEN c."LastVisitDate" IS NULL THEN NULL
    ELSE ((c."LastVisitDate" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow')) END
FROM "Branches" b
WHERE b."CompanyId" = c."CompanyId"
  AND b."Id" = (SELECT MIN("Id") FROM "Branches" WHERE "CompanyId" = c."CompanyId");

UPDATE "OrderStatusHistories" osh SET
  "StartTime" = ((osh."StartTime" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow')),
  "EndTime" = CASE WHEN osh."EndTime" IS NULL THEN NULL
    ELSE ((osh."EndTime" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow')) END
FROM "Orders" o JOIN "Branches" b ON b."Id" = o."BranchId"
WHERE o."Id" = osh."OrderId";

UPDATE "OrderTimelineEntries" ote SET
  "Timestamp" = ((ote."Timestamp" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow'))
FROM "Orders" o JOIN "Branches" b ON b."Id" = o."BranchId"
WHERE o."Id" = ote."OrderId";

UPDATE "ShiftTimelineEntries" ste SET
  "Timestamp" = ((ste."Timestamp" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow'))
FROM "Shifts" s JOIN "Branches" b ON b."Id" = s."BranchId"
WHERE s."Id" = ste."ShiftId";

UPDATE "OrderExpenses" oe SET
  "CreatedAt" = ((oe."CreatedAt" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow'))
FROM "Orders" o JOIN "Branches" b ON b."Id" = o."BranchId"
WHERE o."Id" = oe."OrderId";

UPDATE "OrderServiceItems" osi SET
  "CreatedAt" = ((osi."CreatedAt" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow'))
FROM "Orders" o JOIN "Branches" b ON b."Id" = o."BranchId"
WHERE o."Id" = osi."OrderId";

UPDATE "CashReconciliations" cr SET
  "CountedAt" = ((cr."CountedAt" AT TIME ZONE 'UTC') AT TIME ZONE COALESCE(NULLIF(b."TimeZoneId",''),'Europe/Moscow'))
FROM "Shifts" s JOIN "Branches" b ON b."Id" = s."BranchId"
WHERE s."Id" = cr."ShiftId";

-- НЕ ТРОГАТЬ: "OutboxMessages" (CreatedAtUtc/ProcessedAtUtc уже честный UTC)

## Пост-чеки
1. SELECT "Id", "Time", "Time" AT TIME ZONE 'Europe/Moscow' AS local FROM "Orders" ORDER BY "Id" DESC LIMIT 5;
   — local должен совпадать с тем настенным временем, что видел пользователь до миграции.
2. UI: существующие заказы/смены отображаются тем же настенным временем, что до миграции.
3. Новый заказ: UI = время на часах; в БД = это время минус offset зоны.
4. Отчёты за исторические дни: те же цифры, что до миграции.

## Откат (если миграция ошибочна)
Обратная формула: ((col AT TIME ZONE <zone>) AT TIME ZONE 'UTC')
— применить теми же UPDATE-блоками с заменой формулы.