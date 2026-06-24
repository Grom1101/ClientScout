# 📋 CLIENT SCOUT — PROJECT PLAN

---

# 🟢 ПРОСТЫМ ЯЗЫКОМ (ДЛЯ МЕНЯ)

## Что мы делаем?

Делаем приложение внутри Telegram (называется Telegram Mini App — как мини-сайт, который открывается прямо в Телеграме). Оно будет делать две вещи:

1. **Искать заказы** — автоматически мониторит Telegram-чаты и сайт Kwork. Когда находит что-то по твоим ключевым словам, показывает карточку в приложении. Ты видишь описание заказа и можешь нажать "Написать клиенту".

2. **Рассылать сообщения** — ты пишешь шаблон ("Привет, я фронтенд-разработчик..."), указываешь список чатов, нажимаешь "Запустить" — и приложение само отправляет сообщение с паузами между отправками, чтобы не забанили.

## Мультипрофили — зачем?

Допустим, ты ищешь заказы и как фронтендер, и как бэкендер. Создаёшь два профиля с разными ключевыми словами. Переключаешься между ними в шапке приложения. Они полностью изолированы — разные заказы, разные рассылки, разные чаты.

## Из чего состоит?

```
Приложение = Фронтенд (то, что ты видишь в Telegram) + Бэкенд (мозги, работает на сервере)

Фронтенд: сайт на React (JS) — открывается внутри Telegram как Mini App
Бэкенд: сервер на C# — следит за чатами, ищет заказы, хранит данные
База данных: PostgreSQL — хранит все твои профили, заказы, шаблоны
```

## Что сейчас делаем (MVP — первая версия)?

✅ Только для тебя одного
✅ Только Telegram-чаты + Kwork
✅ Дизайн — простые кнопки, без красоты
✅ База данных — на твоём компьютере (локально)
✅ Сервер — Oracle Cloud (бесплатно навсегда)

Потом, когда убедишься что всё работает:
- Добавим красивый дизайн
- Откроем для других (SaaS с тарифами)
- Добавим Upwork, Viber, WhatsApp и другие источники
- Переедем на нормальный платный сервер

## Про бесплатный сервер

**Oracle Cloud "Always Free"** — лучший вариант для тебя:
- Бесплатно НАВСЕГДА (не 12 месяцев, а реально навсегда пока аккаунт жив)
- Даёт виртуальную машину (4 ядра, 24 ГБ RAM) — это много, хватит с запасом
- 200 ГБ диска + 10 ТБ трафика в месяц
- Нужно только завести аккаунт Oracle (понадобится карта для верификации, деньги не спишутся)

---

# 🔧 ТЕХНИЧЕСКИЕ ДЕТАЛИ

## Принятые решения

| Вопрос | Решение |
|---|---|
| Источники MVP | Telegram-чаты + Kwork (только) |
| Другие мессенджеры | Архитектура расширяемая — Viber/WhatsApp/etc добавим позже |
| Другие биржи | Аналогично — Upwork и другие добавим позже |
| Парсинг Kwork | RSS-фид (простой) + HTTP-запросы. Не нужны сторонние прокси на старте |
| Парсинг Telegram | WTelegramClient (MTProto) — читает чаты как настоящий пользователь |
| Рабочие аккаунты | Да — менеджер Userbot-сессий: добавляешь доп. Telegram-аккаунт через номер + SMS |
| Viber | Пропускаем — архитектура готова к добавлению |
| Бизнес-модель | Internal (только для себя) → потом SaaS. Код пишем так, чтобы легко расширить |
| Хостинг | Oracle Cloud Always Free (бесплатно навсегда) |
| БД | PostgreSQL локально у тебя. Потом строка подключения меняется на сервер — 1 строчка кода |
| Дизайн | Минимальный на старте (кнопки, тексты). Красоту делаем в конце |
| Авторизация | Через Telegram — пользователь открывает приложение и сразу авторизован |

---

## 1. Цель проекта

**Client Scout** — Telegram Mini App для автоматизации поиска фриланс-заказов и рассылки питчей.

**MVP фокус:** Telegram-чаты + Kwork, один пользователь, локальная БД, без дизайна.

---

## 2. Технологический стек

### Frontend

| Категория | Выбор | Причина |
|---|---|---|
| Язык | TypeScript | Меньше ошибок, автодополнение |
| Фреймворк | React 18 | Стандарт для Telegram Mini Apps |
| Сборщик | Vite 6 | Быстрый запуск и сборка |
| TMA SDK | @telegram-apps/sdk-react | Официальный SDK Telegram |
| Стилизация | Vanilla CSS + CSS Modules | Минимально сначала, потом расширяем |
| WebSocket | @microsoft/signalr | Real-time обновление ленты |
| Состояние | Zustand | Простой стейт-менеджер |
| HTTP | Axios | Запросы к бэкенду |

### Backend

| Категория | Выбор | Причина |
|---|---|---|
| Язык | C# 12 / .NET 9 | Требование проекта |
| Web API | ASP.NET Core 9 | Высокая производительность |
| ORM | Entity Framework Core 9 | Code-First, простые миграции |
| СУБД | PostgreSQL 16 | Локально → потом сервер (1 строка конфига) |
| Очереди | Hangfire | Фоновые задачи (парсинг, рассылка), работает 24/7 |
| Real-time | ASP.NET Core SignalR | Push новых лидов без перезагрузки |
| Telegram MTProto | WTelegramClient | Чтение чатов + рассылка от Userbot |
| Парсинг Kwork | HttpClientFactory + AngleSharp | RSS + HTML-парсер |

### Инфраструктура (бесплатно)

| Компонент | Выбор | Стоимость |
|---|---|---|
| Сервер | Oracle Cloud Always Free (4 CPU, 24 GB RAM) | Бесплатно навсегда |
| БД | PostgreSQL локально / на том же Oracle сервере | Бесплатно |
| SSL | Let's Encrypt (Certbot) | Бесплатно |
| Reverse proxy | Nginx | Бесплатно |
| Деплой | GitHub Actions | Бесплатно |

---

## 3. Архитектура

### Как всё работает (упрощённо)

```
Telegram Mini App (фронтенд React)
        |
        | HTTP/WebSocket
        |
   C# API-сервер (ASP.NET Core)
        |              |
  PostgreSQL    Hangfire (фоновые задачи)
  (хранит        |             |
   данные)  Парсинг        Рассылка
             Telegram       (WTelegramClient)
             + Kwork
```

### Почему такая структура легко расширяется

Каждый источник данных (Telegram, Kwork) — это отдельный класс `ISourceScraper`. Чтобы добавить Upwork — создаёшь новый класс, регистрируешь в DI. Всё остальное работает само.

### Структура C# Solution

```
ClientScout/
├── src/
│   ├── ClientScout.Api/              # Web API: контроллеры, SignalR-хабы, middleware
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs     # POST /api/auth/telegram
│   │   │   ├── ProfilesController.cs # CRUD профилей
│   │   │   ├── SourcesController.cs  # CRUD источников
│   │   │   ├── LeadsController.cs    # GET лента лидов, PATCH статус
│   │   │   └── OutreachController.cs # CRUD кампаний, запуск/стоп
│   │   ├── Hubs/
│   │   │   └── LeadsHub.cs           # SignalR: push новых лидов
│   │   ├── Middleware/
│   │   │   ├── JwtMiddleware.cs
│   │   │   └── ExceptionMiddleware.cs
│   │   └── Program.cs
│   │
│   ├── ClientScout.Domain/           # Сущности, интерфейсы
│   │   ├── Entities/
│   │   │   ├── User.cs
│   │   │   ├── Profile.cs
│   │   │   ├── Source.cs             # ISourceScraper — расширяемый интерфейс
│   │   │   ├── JobLead.cs
│   │   │   └── OutreachCampaign.cs
│   │   ├── Interfaces/
│   │   │   ├── ISourceScraper.cs     # Каждый новый источник реализует этот интерфейс
│   │   │   ├── ILeadRepository.cs
│   │   │   └── IOutreachService.cs
│   │   └── Errors/
│   │       └── DomainErrors.cs
│   │
│   ├── ClientScout.Application/      # Use-cases (бизнес-логика)
│   │   ├── Auth/
│   │   ├── Profiles/
│   │   ├── Sources/
│   │   ├── Leads/
│   │   └── Outreach/
│   │
│   ├── ClientScout.Infrastructure/   # БД, Redis, шифрование
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   └── Repositories/
│   │   └── Encryption/
│   │       └── AesEncryptionService.cs
│   │
│   └── ClientScout.Scrapers/         # Воркеры парсинга
│       ├── Abstractions/
│       │   └── ISourceScraper.cs     # Интерфейс — добавь новый класс для нового источника
│       ├── Telegram/
│       │   ├── TelegramScraperService.cs   # WTelegramClient, читает чаты
│       │   └── TelegramOutreachService.cs  # Отправка сообщений
│       ├── Kwork/
│       │   └── KworkScraperService.cs      # RSS + HTTP
│       └── Jobs/
│           ├── ScrapeJob.cs          # Hangfire: запускает все скраперы по расписанию
│           └── OutreachJob.cs        # Hangfire: выполняет рассылку
│
├── tests/
│   ├── ClientScout.UnitTests/
│   └── ClientScout.IntegrationTests/
│
├── frontend/                         # React приложение
│   ├── src/
│   │   ├── api/
│   │   │   ├── client.ts
│   │   │   ├── leadsApi.ts
│   │   │   ├── profilesApi.ts
│   │   │   └── outreachApi.ts
│   │   ├── components/
│   │   │   ├── LeadCard/
│   │   │   ├── ProfileSwitcher/
│   │   │   └── StatusBadge/
│   │   ├── pages/
│   │   │   ├── ScoutFeed/
│   │   │   ├── ProfileSettings/
│   │   │   ├── SourcesManager/
│   │   │   └── OutreachManager/
│   │   ├── store/
│   │   │   ├── authStore.ts
│   │   │   ├── profileStore.ts
│   │   │   └── leadsStore.ts
│   │   ├── hooks/
│   │   │   ├── useSignalR.ts
│   │   │   └── useTelegramApp.ts
│   │   ├── styles/
│   │   │   ├── variables.css
│   │   │   └── globals.css
│   │   └── App.tsx
│   ├── index.html
│   ├── vite.config.ts
│   └── package.json
│
└── docker-compose.yml
```

---

## 4. Модель данных (PostgreSQL)

Вся схема хранится в EF Core миграциях. Строка подключения — в переменной окружения. Поменять с локальной на серверную = сменить 1 строку в `.env`.

```sql
-- Пользователь
CREATE TABLE users (
  id             BIGINT PRIMARY KEY,          -- Telegram User ID
  username       VARCHAR(255),
  first_name     VARCHAR(255),
  created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  subscription   VARCHAR(50) NOT NULL DEFAULT 'free'  -- free | pro | enterprise (на будущее SaaS)
);

-- Профиль
CREATE TABLE profiles (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id        BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  name           VARCHAR(100) NOT NULL,
  color          VARCHAR(7),                  -- HEX цвет
  is_active      BOOLEAN NOT NULL DEFAULT true,
  is_default     BOOLEAN NOT NULL DEFAULT false,
  keywords       TEXT[] NOT NULL DEFAULT '{}',
  negative_kw    TEXT[] NOT NULL DEFAULT '{}',
  min_budget     DECIMAL(10,2),
  lang_filter    VARCHAR(10),
  created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Источник (Telegram-чат или Kwork)
CREATE TABLE sources (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  profile_id     UUID NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
  type           VARCHAR(50) NOT NULL,        -- 'telegram' | 'kwork' | 'upwork' (на будущее)
  name           VARCHAR(255),
  url            TEXT NOT NULL,
  chat_id        BIGINT,
  credentials    TEXT,                       -- AES-256 зашифрованные данные сессии
  status         VARCHAR(50) DEFAULT 'pending',  -- pending | active | error
  last_error     TEXT,
  last_scraped   TIMESTAMPTZ,
  created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Найденный лид (заказ)
CREATE TABLE job_leads (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  profile_id     UUID NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
  source_id      UUID NOT NULL REFERENCES sources(id),
  external_id    TEXT NOT NULL,
  title          VARCHAR(500),
  content        TEXT NOT NULL,
  original_url   TEXT NOT NULL,
  author_url     TEXT,
  budget         DECIMAL(10,2),
  status         VARCHAR(50) DEFAULT 'new',  -- new | viewed | responded | hidden
  matched_kw     TEXT[],
  found_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE (source_id, external_id)
);

-- Шаблон сообщения
CREATE TABLE message_templates (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  profile_id     UUID NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
  name           VARCHAR(255) NOT NULL,
  content        TEXT NOT NULL,
  created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Кампания рассылки
CREATE TABLE outreach_campaigns (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  profile_id     UUID NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
  template_id    UUID NOT NULL REFERENCES message_templates(id),
  target_chats   JSONB NOT NULL,
  delay_min_sec  INT NOT NULL DEFAULT 30,
  delay_max_sec  INT NOT NULL DEFAULT 90,
  status         VARCHAR(50) DEFAULT 'draft',
  sent_count     INT NOT NULL DEFAULT 0,
  error_count    INT NOT NULL DEFAULT 0,
  current_index  INT NOT NULL DEFAULT 0,     -- Для возобновления рассылки
  started_at     TIMESTAMPTZ,
  finished_at    TIMESTAMPTZ,
  created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Лог рассылки
CREATE TABLE outreach_logs (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  campaign_id    UUID NOT NULL REFERENCES outreach_campaigns(id) ON DELETE CASCADE,
  chat_id        BIGINT,
  chat_name      VARCHAR(255),
  status         VARCHAR(50),               -- sent | error | skipped
  error_message  TEXT,
  sent_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Userbot-сессии (рабочие аккаунты для рассылок)
CREATE TABLE userbot_sessions (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id        BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  phone          VARCHAR(20) NOT NULL,
  session_data   TEXT NOT NULL,             -- AES-256 зашифрованная WTelegramClient сессия
  display_name   VARCHAR(255),
  is_active      BOOLEAN NOT NULL DEFAULT true,
  created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

## 5. API-контракты

### Auth
```
POST /api/auth/telegram
  Body: { "initData": "строка от Telegram Web App" }
  Response: { accessToken, refreshToken, expiresIn, user }

POST /api/auth/refresh
  Body: { "refreshToken": "..." }
  Response: { accessToken, expiresIn }
```

### Профили
```
GET    /api/profiles
POST   /api/profiles
GET    /api/profiles/{id}
PUT    /api/profiles/{id}
DELETE /api/profiles/{id}
PATCH  /api/profiles/{id}/activate
```

### Источники
```
GET    /api/profiles/{profileId}/sources
POST   /api/profiles/{profileId}/sources
DELETE /api/profiles/{profileId}/sources/{id}
PATCH  /api/profiles/{profileId}/sources/{id}/refresh
```

### Лиды
```
GET    /api/profiles/{profileId}/leads?status=new&page=1&pageSize=20
PATCH  /api/leads/{id}/status   Body: { "status": "responded" }
DELETE /api/leads/{id}
```

### Рассылки
```
GET    /api/profiles/{profileId}/campaigns
POST   /api/profiles/{profileId}/campaigns
PUT    /api/profiles/{profileId}/campaigns/{id}
PATCH  /api/profiles/{profileId}/campaigns/{id}/start
PATCH  /api/profiles/{profileId}/campaigns/{id}/pause
PATCH  /api/profiles/{profileId}/campaigns/{id}/stop
GET    /api/campaigns/{id}/logs?page=1&pageSize=50
```

### SignalR Hub: /hubs/leads
```
Клиент подписывается на группу по profileId.
Сервер отправляет события:
  - "NewLead"             -> { lead: JobLeadDto }
  - "SourceStatusChanged" -> { sourceId, status, error? }
  - "CampaignProgress"    -> { campaignId, sentCount, status }
```

---

## 6. Безопасность

| Угроза | Меры защиты |
|---|---|
| Подделка Telegram initData | HMAC-SHA256 с Bot Token + проверка auth_date (не старше 600 сек) |
| Кража JWT | access token живёт 1 час, refresh — 7 дней |
| Доступ к чужим данным | Все запросы фильтруются по userId из JWT |
| Хранение Userbot-сессий | AES-256-GCM; ключ только в env-переменной |
| SQL-инъекции | Только параметризованные запросы EF Core |
| Секреты | Только в .env файле, никогда в коде и в Git |

---

## 7. UI/UX — Экраны (MVP минимальный дизайн)

```
Splash Screen
  |
Main App
  ├── [ProfileSwitcher] — чипы в шапке (переключение профилей)
  |
  ├── Tab: Scout (лента лидов)
  |   ├── FilterBar (All / New / Responded)
  |   └── LeadCard
  |       ├── Заголовок + превью текста
  |       ├── Источник (иконка Telegram/Kwork) + дата
  |       └── Кнопки: [Написать] [Скрыть] [Откликнулся]
  |
  ├── Tab: Outreach (рассылки)
  |   ├── CampaignList (список кампаний + статус)
  |   ├── CampaignEditor (текст, чаты, задержка)
  |   └── CampaignLog (лог отправки)
  |
  └── Tab: Settings
      ├── ProfileEditor (имя, цвет)
      ├── KeywordsEditor (ключевые слова, минус-слова)
      ├── SourcesManager (добавить/удалить чаты и Kwork)
      └── UserbotManager (добавить рабочий TG-аккаунт через номер телефона)
```

---

## 8. Конфигурация

### .env (никогда не коммитить в Git!)
```env
# База данных (меняем с локальной на сервер когда будем запускать SaaS)
POSTGRES_CONNECTION=Host=localhost;Database=clientscout;Username=postgres;Password=...

# Telegram
TELEGRAM_BOT_TOKEN=...
TMA_ENCRYPTION_KEY=<32 случайных байта — для шифрования сессий>

# JWT
JWT_SECRET=<длинная случайная строка>
JWT_ISSUER=clientscout.app
JWT_AUDIENCE=clientscout.tma

# Hangfire (отдельная БД для очередей задач)
HANGFIRE_DB=Host=localhost;Database=clientscout_hangfire;Username=postgres;Password=...
```

---

## 9. Пошаговый план разработки (Roadmap)

### Фаза 1: MVP (только для тебя, Telegram + Kwork)

| # | Что делаем | Ориентир |
|---|---|---|
| 1 | Создаём структуру проекта: C# Solution + React Vite + Docker Compose | Старт |
| 2 | Создаём схему БД PostgreSQL + EF Core миграции | |
| 3 | Авторизация: Telegram initData -> JWT токен | |
| 4 | CRUD профилей (фронт + бэк) | |
| 5 | CRUD источников: добавить Telegram-чат, добавить Kwork | |
| 6 | Скрапер Telegram (WTelegramClient): читает чаты, фильтрует по ключевым словам, сохраняет лиды | |
| 7 | Скрапер Kwork: RSS + HTTP-парсинг новых заказов | |
| 8 | Лента лидов с фильтрацией | |
| 9 | SignalR: новые лиды появляются в реальном времени | |
| 10 | Рассылки: шаблон + кампания + Hangfire-воркер | |
| 11 | Userbot-менеджер: добавить доп. TG-аккаунт (номер + SMS) | |
| 12 | Деплой на Oracle Cloud (сервер) + Nginx + Let's Encrypt SSL | |
| 13 | Smoke-тест всего: поиск реальный + рассылка реальная | |

### Фаза 2: Расширение

| Задача |
|---|
| Upwork-парсер (добавляем новый класс ISourceScraper) |
| AI-генератор питча (OpenAI API по описанию заказа) |
| Уведомления в Telegram-бот при новых лидах |
| Статистика и аналитика |
| Красивый дизайн (Glassmorphism, анимации) |

### Фаза 3: SaaS

| Задача |
|---|
| Система тарифов (Free / Pro / Enterprise) |
| Биллинг (Telegram Stars или Stripe) |
| Перенос БД на продакшн сервер (1 строка в .env) |
| Viber, WhatsApp и другие мессенджеры |
| Командный режим |

---

## 10. Риски

| Риск | Насколько вероятно | Что делаем |
|---|---|---|
| Telegram забанит аккаунт за рассылку | Высокая вероятность без защиты | Userbot-менеджер + задержки 30–90 сек + лимиты в день |
| Kwork заблокирует парсинг | Средняя | RSS-фид как основа + ротация User-Agent |
| WTelegramClient устареет | Низкая | Мониторим, есть замена TDLib |

---

## 11. Definition of Done (MVP готов когда...)

- [ ] Создал профиль → добавил чат → задал ключевые слова → появились реальные лиды
- [ ] Новые лиды появляются в реальном времени (без перезагрузки страницы)
- [ ] Нажал "Написать" → открылась правильная ссылка на заказчика
- [ ] Рассылка по чатам работает с задержками
- [ ] Приложение открывается в Telegram и работает
- [ ] Данные твои — никто другой их не видит
