---
name: ✨ Предложение функциональности
about: Предложить идею для улучшения системы
title: "[FEATURE] "
labels: enhancement
assignees: ''
---

## 🎯 Какую проблему вы хотите решить?

Опишите проблему, которую решает ваше предложение. Например: "Мне приходится делать X вручную, это занимает 10 минут в день".

## 💡 Предлагаемое решение

Опишите, как вы видите решение.

## 🔄 Альтернативы

Рассматривали ли вы другие варианты? Почему они не подошли?

## 📊 Бизнес-ценность

- Сколько пользователей это затронет?
- Какую выгоду это принесёт (время / деньги / UX)?
- Связано ли это с каким-то DLC-модулем (Upsell / CRM / Reputation)?

## 🎨 UI/UX (если применимо)

- Где должна быть новая функциональность (какое окно/экран)?
- Есть ли примеры у конкурентов?
- Можете набросать mockup?

## 📋 Дополнительная информация

Любой контекст, ссылки, скриншоты.
```

---

## 📄 5. `.github/PULL_REQUEST_TEMPLATE.md`

```markdown
## 📋 Описание

Кратко опишите, что делает этот PR.

Fixes # (номер issue, если есть)

## 🔄 Тип изменений

- [ ] ✨ Новая функциональность (non-breaking change)
- [ ] 🐛 Исправление бага (non-breaking change)
- [ ] 💥 Breaking change (требует миграции БД / изменения API)
- [ ] 📝 Документация
- [ ] ♻️ Рефакторинг (без изменения поведения)
- [ ] 🎨 Стиль/форматирование
- [ ] ✅ Тесты

## 🎯 Затронутые компоненты

- [ ] `AccuratSystem.Contracts` (netstandard2.0 — проверяйте совместимость!)
- [ ] `Accurat.WebAPI` (сервер)
- [ ] `AccuratPanelCWD` (WPF, .NET 10)
- [ ] `AccuratPanelCWM` (MAUI)
- [ ] `AccuratPanelCarWashing` (legacy WPF, .NET Framework 4.6.2)
- [ ] `docs/` (документация)

## ✅ Чек-лист перед отправкой

- [ ] Я прочитал [CONTRIBUTING.md](CONTRIBUTING.md)
- [ ] Все коммиты содержат `Signed-off-by` (DCO)
- [ ] Код соответствует стилю проекта
- [ ] Новые публичные API покрыты XML-документацией
- [ ] Новые эндпоинты проверяют `CurrentCompanyId` (SaaS-изоляция)
- [ ] Использую `DateTime.UtcNow` вместо `DateTime.Now`
- [ ] Не использую `DateTime.SpecifyKind` (только `BusinessTime.ToInstantUtc`)
- [ ] Решение собирается без ошибок
- [ ] Все клиенты запускаются (CWD, CWM, CarWashing, WebAPI)
- [ ] Изменения не ломают совместимость с .NET Framework 4.6.2
- [ ] Добавлены миграции БД (если изменилась схема)
- [ ] Обновлена документация (если применимо)

## 🧪 Как проверить

1. Открыть '...'
2. Нажать '...'
3. Увидеть '...'

## 📸 Скриншоты (для UI-изменений)

| До | После |
|----|-------|
| ![before](url) | ![after](url) |

## 📝 Миграция БД

Если PR содержит миграцию, укажите SQL-скрипт:

```sql
-- сюда вставьте SQL
```

## 🔗 Связанные issues / PR

- Closes #...
- Related to #...

## 📌 Примечания для ревьюера

Любой дополнительный контекст, на который стоит обратить внимание.
```

---

## 📝 Коммит для всех файлов

Сохраните каждый файл в репозиторий, затем:

```bash
git add SECURITY.md CONTRIBUTING.md .github/
git commit -m "docs: добавлены SECURITY.md, CONTRIBUTING.md и шаблоны issues/PR для BSL-модели

- SECURITY.md (EN): процесс responsible disclosure, SLA 48h/7d/30d,
  scope (API/клиенты/БД), правила для контрибьюторов
- CONTRIBUTING.md (RU): стиль кода, Conventional Commits, DCO
  (Signed-off-by в каждом коммите), правила для BSL, архитектурные
  ограничения (netstandard2.0 совместимость, .NET Framework 4.6.2 legacy)
- .github/ISSUE_TEMPLATE/bug_report.md: обязательные поля (клиент,
  версия, шаги, логи)
- .github/ISSUE_TEMPLATE/feature_request.md: бизнес-ценность, UI/UX
- .github/PULL_REQUEST_TEMPLATE.md: чек-лист (SaaS-изоляция,
  DateTime.UtcNow, DCO, совместимость с legacy-клиентом)

Signed-off-by: Your Name <your.email@example.com>"
git push origin main
```