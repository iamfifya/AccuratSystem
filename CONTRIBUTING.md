# Как внести вклад в AccuratSystem

Спасибо за интерес к проекту! AccuratSystem распространяется под лицензией **BSL 1.1 (Business Source License)**. Это означает, что код открыт для изучения и аудита, но коммерческое использование требует отдельной лицензии. Мы приветствуем вклад сообщества и готовы принимать pull requests при соблюдении правил ниже.

## 📋 Перед началом

1. **Прочитайте `README.md`** — там описана архитектура и назначение компонентов
2. **Изучите `docs/TIME_CONVENTIONS.md`** — критически важный документ, описывающий правила работы со временем (конвенция v2)
3. **Проверьте открытые issues** — возможно, ваша задача уже решается
4. **Для багов** используйте шаблон [Bug Report](.github/ISSUE_TEMPLATE/bug_report.md)
5. **Для идей** используйте шаблон [Feature Request](.github/ISSUE_TEMPLATE/feature_request.md)

## 🔄 Процесс внесения вклада

### 1. Форк и ветка

```bash
git clone https://github.com/YOUR_USERNAME/AccuratSystem.git
cd AccuratSystem
git checkout -b feature/your-feature-name
# или
git checkout -b fix/issue-123-description
```

**Правила именования веток:**
- `feature/` — новая функциональность
- `fix/` — исправление бага
- `refactor/` — рефакторинг без изменения поведения
- `docs/` — изменения в документации
- `test/` — добавление тестов

### 2. Разработка

#### Стиль кода

Мы придерживаемся стандартных конвенций C#:

- **Именование:**
  - `PascalCase` для типов, свойств, методов
  - `camelCase` для параметров и локальных переменных
  - `_camelCase` для приватных полей
  - `UPPER_SNAKE_CASE` для констант

- **Форматирование:**
  - Фигурные скобки на новой строке (Allman style)
  - Отступы — 4 пробела (не табы)
  - Максимум одна пустая строка подряд

- **Обязательно:**
  - XML-документация для публичных методов в `AccuratSystem.Contracts`
  - Проверка `CurrentCompanyId` во всех новых API-эндпоинтах (SaaS-изоляция)
  - Использование `DateTime.UtcNow` для всех серверных таймстампов
  - Никаких `DateTime.SpecifyKind` — только `BusinessTime.ToInstantUtc` для входящих дат

#### Что нужно знать про архитектуру

- **`AccuratSystem.Contracts`** — netstandard2.0, используется ВСЕМИ клиентами (WPF, MAUI, .NET Framework 4.6.2). Не добавляйте сюда зависимости, несовместимые с netstandard2.0
- **`AccuratPanelCarWashing`** — legacy WPF на .NET Framework 4.6.2. Любое изменение контрактов должно оставаться совместимым с ним
- **`Accurat.WebAPI`** — сервер, использует NodaTime для работы со временем и зонами
- **Модели в контрактах** одновременно являются EF-сущностями и wire-DTO — изменение модели = изменение схемы БД + API

### 3. Тестирование

Перед созданием PR убедитесь:

- ✅ Решение собирается без ошибок (`Build Solution` в VS)
- ✅ Все 4 клиента запускаются (CWD, CWM, CarWashing, WebAPI)
- ✅ Новые эндпоинты API покрыты ручным тестом через Swagger
- ✅ UI-изменения проверены в светлой и тёмной теме (если применимо)

### 4. Коммиты

Используйте [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <description>

[optional body]

Signed-off-by: Your Name <your.email@example.com>
```

**Типы:**
- `feat` — новая функциональность
- `fix` — исправление бага
- `refactor` — рефакторинг
- `docs` — документация
- `style` — форматирование
- `test` — тесты
- `chore` — инфраструктура

**Области (scope):**
- `time` — работа со временем
- `api` — серверный API
- `wpf` / `maui` / `carwash` — клиенты
- `db` — миграции БД
- `docs` — документация

**Пример:**
```
feat(time): добавлен UI-выбор TimeZoneId в BranchManagementWindow

CustomComboBox с 12 зонами РФ + дефолт Europe/Moscow.
Пустая строка = null в БД = серверный дефолт через BranchZoneResolver.

Signed-off-by: Ivan Petrov <ivan@example.com>
```

### 5. Pull Request

- Создайте PR из вашей ветки в `main`
- Используйте шаблон [Pull Request](.github/PULL_REQUEST_TEMPLATE.md)
- Заполните все пункты чек-листа
- Дождитесь ревью от мейнтейнеров

## 📝 Developer Certificate of Origin (DCO)

**Обязательное требование:** каждый коммит должен содержать строку `Signed-off-by`, подтверждающую ваше согласие с [DCO](https://developercertificate.org/):

```
Signed-off-by: Your Name <your.email@example.com>
```

**Что это означает:**
- Вы являетесь автором вклада или имеете право его передавать
- Вы предоставляете проекту неисключительную лицензию на использование вашего вклада на условиях BSL 1.1
- Вы понимаете, что ваш вклад станет частью проекта и будет распространяться под BSL 1.1

**Как добавить:**

Git делает это автоматически с флагом `-s`:
```bash
git commit -s -m "feat(api): added new endpoint"
```

Или настройте алиас в `~/.gitconfig`:
```ini
[alias]
    cs = commit -s
```

**PR без Signed-off-by в коммитах будут отклонены.**

## 🚫 Что мы не принимаем

- Контрибуции без Signed-off-by (DCO)
- Изменения, ломающие совместимость с .NET Framework 4.6.2 клиентом
- Зависимости, несовместимые с netstandard2.0, в `AccuratSystem.Contracts`
- Использование `DateTime.SpecifyKind`, `DateTime.Now` в серверном коде
- Изменения в финансовых расчётах (`OrderMath`) без согласования с мейнтейнерами
- Копипаст кода без атрибуции

## 💼 Коммерческое использование

Код доступен для:
- ✅ Изучения и аудита
- ✅ Тестирования в некоммерческих целях
- ✅ Fork для личных экспериментов

Для коммерческого использования (в том числе модифицированных версий) требуется коммерческая лицензия. Свяжитесь с нами: **sales@accurat.systems**

## 📞 Контакты

- Вопросы по коду: создайте issue с тегом `question`
- Баги: используйте шаблон [Bug Report](.github/ISSUE_TEMPLATE/bug_report.md)
- Безопасность: **security@accurat.systems** (не создавайте issue!)
- Коммерческие вопросы: **dimakuraedov@gmail.com**

---

Спасибо за ваш вклад! 🚀