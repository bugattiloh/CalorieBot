# CalorieBot — Telegram-бот контроля питания

Бот следит за дневным максимумом калорий: я задаю лимит, записываю съеденное и вижу,
сколько ещё можно съесть сегодня — и что именно из моих любимых продуктов ещё вписывается в остаток.

Управление **полностью кнопочное**: единственная команда — `/start` для инициализации,
дальше только ReplyKeyboard (меню) и InlineKeyboard (выбор продуктов, типов приёма пищи, подтверждения).

## Возможности

- **Дневной лимит калорий.** При первом запуске — 2000 ккал; действует постоянно, пока я сам его не заменю.
- **Прогресс за день.** «Съедено X из Y ккал (Z%). Осталось: N ккал», полоска прогресса, БЖУ,
  время до сброса счётчика и список избранного, которое влезает в остаток. Перебор подсвечивается ⚠️.
- **Запись приёмов пищи** — из избранного в два тапа или новым продуктом (название → БЖУ → калории считаются сами).
- **Избранные продукты** — добавление с БЖУ и размером порции, постраничный просмотр, удаление с подтверждением.
- **История за сегодня** — с группировкой по завтраку / обеду / ужину / перекусу.
- **Сброс дневного счётчика** — в полночь по UTC+3 (счётчик не «чистится», а считается по границам дня).

## Меню

| Экран | Кнопки |
|---|---|
| Главное меню | 🍽 Добавить прием пищи · 📊 Мой прогресс · ⭐ Любимые продукты · 🎯 Дневной лимит · 📋 История сегодня |
| Добавить прием пищи | 💝 Из любимых · 🔍 Новый продукт · 🔙 Назад |
| Любимые продукты | ➕ Добавить в избранное · 📝 Мои продукты · 🗑 Удалить из избранного · 🔙 Назад |
| Дневной лимит | ✏️ Изменить лимит калорий · 📊 Текущий лимит · 🔙 Назад |
| Инлайн | 🍳 Завтрак · 🍲 Обед · 🍝 Ужин · 🍎 Перекус · ✅ Сохранить в избранное · ❌ Нет, спасибо · 🔄 Добавить еще · ◀️ Назад · Вперед ▶️ · 🔙 В меню |

## Стек

- .NET 8.0, ASP.NET Core (минимальный хост: health check + фоновая служба поллинга)
- [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) 22.0
- PostgreSQL 16 + Entity Framework Core 8 (Npgsql), миграции
- Docker + docker-compose

## Структура проекта

```
CalorieBot/
├── src/
│   ├── CalorieBot.Api/     # хост, хендлеры Telegram, клавиатуры, тексты, health check
│   │   ├── Bot/
│   │   │   ├── Scenarios/  # пошаговые диалоги: еда, избранное, лимит, прогресс
│   │   │   ├── UI/         # кнопки, клавиатуры, коды callback, тексты сообщений
│   │   │   ├── BotHostedService.cs   # поллинг + graceful shutdown
│   │   │   ├── BotUpdateHandler.cs   # scope на апдейт + глобальная обработка ошибок
│   │   │   └── UpdateRouter.cs       # маршрутизация нажатий
│   │   ├── Configuration/  # BotOptions с валидацией на старте
│   │   └── Infrastructure/ # миграции при старте, health checks, статус бота
│   ├── CalorieBot.Core/    # бизнес-логика: сервисы, расчёт калорий, день UTC+3, валидация, состояния диалогов
│   └── CalorieBot.Data/    # EF Core: сущности, контекст, миграции
├── docker-compose.yml
├── Dockerfile
├── .env.example
└── README.md
```

Зависимости слоёв линейные: `Api → Core → Data`.

## Схема базы данных

**Users**

| Колонка | Тип | Примечание |
|---|---|---|
| UserId | bigint PK | id из Telegram |
| Username / FirstName | text | обновляются при каждом обращении |
| DailyCalorieLimit | int NOT NULL DEFAULT 2000 | дневной максимум |
| DailyProteinsLimit / DailyFatsLimit / DailyCarbsLimit | numeric(7,2) | ориентиры БЖУ (30/30/40 % от лимита) |
| CreatedAt | timestamptz DEFAULT now() | |
| GoalSetAt | timestamptz NULL | когда лимит меняли явно |

**FavoriteProducts**

| Колонка | Тип | Примечание |
|---|---|---|
| Id | serial PK | |
| UserId | bigint FK → Users | ON DELETE CASCADE |
| Name | text NOT NULL | уникален в паре с UserId |
| Calories | int NOT NULL | считается из БЖУ |
| Proteins / Fats / Carbs | numeric(7,2) DEFAULT 0 | |
| ServingSize | text NULL | «200 г», «1 стакан» |
| CreatedAt | timestamptz DEFAULT now() | |

**FoodLog**

| Колонка | Тип | Примечание |
|---|---|---|
| Id | serial PK | |
| UserId | bigint FK → Users | ON DELETE CASCADE |
| ProductName, Calories, Proteins, Fats, Carbs, ServingSize | | копия КБЖУ на момент записи |
| MealType | int | 1 завтрак, 2 обед, 3 ужин, 4 перекус |
| LoggedAt | timestamptz DEFAULT now() | UTC; границы дня считаются по UTC+3 |
| IsFavorite | bool DEFAULT false | продукт связан с избранным |
| FavoriteProductId | int NULL FK | ON DELETE SET NULL — история не теряется |

Индексы: `FoodLog (UserId, LoggedAt)` под запрос «что съедено сегодня», `FavoriteProducts (UserId, Name)` UNIQUE против дублей.

## Развёртывание в Docker

### 1. Настроить окружение

```bash
cp .env.example .env
```

В `.env` нужно указать как минимум:

```env
BOT_TOKEN=<токен из BotFather>
POSTGRES_PASSWORD=<пароль для базы>
```

### 2. Запустить

```bash
docker-compose up -d
```

Что произойдёт:

1. поднимется `postgres` с именованным томом `caloriebot-postgres-data`;
2. бот дождётся healthcheck базы (`pg_isready`), накатит EF-миграции и начнёт поллинг;
3. на `http://localhost:8080/health` появится статус сервиса.

### 3. Проверить

```bash
docker-compose ps                 # состояние и health обоих сервисов
docker-compose logs -f bot        # логи в JSON
curl http://localhost:8080/health # postgres + telegram
curl http://localhost:8080/       # имя бота, счётчик обработанных апдейтов
```

В Telegram: открыть бота и отправить `/start`.

### 4. Обновление и остановка

```bash
docker-compose up -d --build      # пересобрать после изменений кода
docker-compose stop               # остановить (graceful, до 30 секунд)
docker-compose down               # остановить и удалить контейнеры (том с данными остаётся)
docker-compose down -v            # удалить и данные тоже
```

### Переменные окружения

| Переменная | По умолчанию | Назначение |
|---|---|---|
| `BOT_TOKEN` | — (обязательно) | токен Telegram → `Bot__Token` |
| `BOT_DROP_PENDING_UPDATES` | `true` | игнорировать апдейты, накопившиеся за время простоя |
| `POSTGRES_DB` / `POSTGRES_USER` | `caloriebot` | база и пользователь |
| `POSTGRES_PASSWORD` | — (обязательно) | пароль |
| `POSTGRES_PORT` | `5432` | порт базы на хосте (только для отладки) |
| `BOT_HTTP_PORT` | `8080` | порт HTTP-эндпоинтов бота |
| `ASPNETCORE_ENVIRONMENT` | `Production` | окружение приложения |

## Локальный запуск без Docker

```bash
# 1. База (например, в контейнере)
docker-compose up -d postgres

# 2. Токен и строка подключения
export Bot__Token="<токен>"
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=caloriebot;Username=caloriebot;Password=<пароль>"

# 3. Запуск
dotnet run --project src/CalorieBot.Api
```

## Миграции EF Core

Миграции применяются автоматически при старте (`DatabaseMigratorHostedService`, до 10 попыток с паузами —
на случай, когда база ещё поднимается). Вручную:

```bash
# создать новую миграцию
dotnet ef migrations add <Name> --project src/CalorieBot.Data --startup-project src/CalorieBot.Api

# применить к работающей базе
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=caloriebot;Username=caloriebot;Password=<пароль>"
dotnet ef database update --project src/CalorieBot.Data --startup-project src/CalorieBot.Api
```

## Как это работает внутри

- **Один scope на апдейт.** `BotUpdateHandler` создаёт scope, внутри живут `DbContext` и сервисы;
  любое исключение логируется и превращается в вежливое сообщение пользователю — поллинг не падает.
- **Состояния диалогов** (`ConversationState`) лежат в `IMemoryCache` со скользящим часом:
  брошенные на середине диалоги вычищаются сами.
- **Кэш** на профиль пользователя и список избранного (10 минут, сбрасывается при любой записи) —
  на каждое нажатие кнопки в базу я не хожу.
- **Калории** считаются один раз в `CalorieCalculator`: `Б×4 + Ж×9 + У×4`.
- **День бота** — `DayClock`: фиксированное смещение UTC+3 (без зависимости от tzdata в контейнере),
  прогресс считается по границам `[00:00; 24:00)` этого дня.
- **Валидация** всего текстового ввода — в `InputParser`: название 2–64 символа, БЖУ — ровно три числа
  (точка или запятая, до 1000 г), лимит — 500–10000 ккал.
- **Логи** — JSON в stdout, со скоупами `UpdateId` / `UpdateType` / `UserId`.
- **Health check** — `/health`: доступность PostgreSQL (`AddDbContextCheck`) и состояние поллинга.
- **Graceful shutdown** — `ReceiveAsync` завершается по `CancellationToken`, у хоста 15 секунд
  на дозавершение, у контейнера — 30.

## Сценарии

1. **Прогресс.** 📊 Мой прогресс → лимит, съедено, остаток, БЖУ, подходящие любимые продукты.
2. **Еда из избранного.** 🍽 → 💝 Из любимых → продукт → тип приёма пищи → запись и обновлённый прогресс.
3. **Новый продукт.** 🍽 → 🔍 Новый продукт → название → БЖУ → бот считает калории →
   «Сохранить в избранное?» → тип приёма пищи → запись.
4. **Смена лимита.** 🎯 Дневной лимит → ✏️ Изменить лимит калорий → число → лимит обновлён.
