# 📋 CLIENT SCOUT — PROJECT PLAN

---

## Codex Tooling Rules — 2026-06-29

При работе над ClientScout использовать доступные плагины/инструменты прагматично:

- **Build/Web App workflow**: для frontend-задач всегда проверять сборку (`npm.cmd run build`) и, когда меняется UI, по возможности проверять приложение в браузере/скриншотом.
- **Chrome / Browser automation**: для визуальных правок, Telegram Mini App UI, свайпов, модалок и адаптива использовать браузерную проверку через доступные browser/Chrome-инструменты или Playwright/node_repl.
- **node_repl**: использовать для быстрых JS/Playwright-проверок, DOM-инспекции, скриншотов и автоматизации браузера, когда это быстрее обычных shell-команд.
- **Codex Security / GitHub**: использовать для security-review, проверки подозрительных изменений, CI/GitHub Actions/PR-задач и потенциально опасных мест: auth, tokens, cookies, sessions, Telegram/Kwork credentials.
- **Computer Use**: использовать только когда обычные shell/browser/API-инструменты не дают результата или нужно вручную пройти локальный UI/браузерный flow.
- **Cloudflare Quick Tunnel**: когда пользователь просит ссылку для Telegram Mini App, запускать/проверять `cloudflared tunnel --url http://localhost:5173`; если QUIC/TCP edge заблокирован, явно сообщать, что tunnel не установил соединение.

Не вызывать плагины механически перед каждым промптом. Инструменты должны применяться там, где они реально улучшают проверку, диагностику или качество ответа.

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

---

## Search MVP — Decisions From 2026-06-27

### Search cadence
- User can choose scan interval: 5, 10, 15, 20, 25, or 30 minutes.
- The interval is profile-level and shared across search sources.
- Source scanning should be queued and jittered to avoid bursts:
  - Telegram can run slightly before the selected interval.
  - Kwork can run slightly after the selected interval.
  - Found leads are processed and notifications are sent through a queue.

### Search sources
- Search chats and outreach chats are separate lists.
- Search sources use `purpose = 0`.
- Outreach sources use `purpose = 1`.
- The same chat may exist in both lists.
- Search chats can be read-only. Search only needs read access, not write access.
- Telegram search reads:
  - channel posts;
  - group messages;
  - admin/bot messages if they look like real job posts.
- Telegram search does not read comments for MVP.
- On first source scan:
  - read only the latest 20 Telegram messages/posts;
  - read only the latest 20 Kwork orders;
  - show matching old leads;
  - send notifications for matching old leads.

### Lead storage and UI
- Found leads are stored for 7 days.
- Leads older than 7 days are physically deleted from DB.
- Search screen shows the latest 10 leads.
- A "show all" / arrow action opens a separate lead history screen with all leads from the 7-day retention window.
- Lead list sorting is by found time only.
- Lead statuses for MVP: `New`, `Viewed`.
- Viewed leads remain in the list and move down only when newer leads arrive.

### Keywords and negative keywords
- User keywords describe the intended search domain, not necessarily exact text to match.
- ClientScout search is universal: keywords may describe any domain or service, including software, repair work, local services, design, content, legal help, marketing, tutoring, hardware repair, construction, or anything else.
- When keywords/negative keywords are saved, AI expansion builds a hidden search profile from those words: what the user is actually looking for, related terms and synonyms, common client wording for that exact domain, strong domain markers, and bilingual equivalents.
- This hidden profile is not shown to the user in MVP, but it is used by the deterministic pre-filter and AI classifier.
- Negative keywords are required in MVP.
- Negative keywords filter out otherwise relevant leads.
- Negative keywords are also domain-neutral: they exclude whatever the user explicitly does not want for the current search profile.

### AI matching approach
- AI is used as a classifier, not as an unlimited web scanner.
- Pipeline:
  1. Normalize raw post/order text.
  2. Apply cheap deterministic pre-filter using user keywords, strong keyword phrases, cached AI-expanded terms, and broad intent phrases.
  3. Apply user negative keyword filtering.
  4. Send only candidate texts to AI.
  5. AI returns structured JSON with relevance, category, summary, and confidence.
  6. Save and notify only if confidence is >= 70%.
- AI must not check every raw message/post.
- AI is called when search settings are saved only if user keywords or negative keywords changed.
- Changing interval or notification toggle must not call AI.
- When keywords/negative keywords change, send the old expanded dictionary plus the exact diff to AI and ask it to rewrite/update the expanded dictionary instead of regenerating from scratch.
- User keywords limit: 20.
- User negative keywords limit: 10.
- Expanded AI terms are hidden from the user in MVP.
- UI should still indicate that AI assistance is used near the keyword settings, for example a small label/badge such as "AI helps refine search".
- If AI quota is unavailable:
  - use the last cached expanded dictionary;
  - save keyword-score matches to the lead list;
  - do not send Telegram notifications for non-AI-confirmed leads.
- Pre-AI candidate threshold for MVP:
  - at least 2 ordinary keyword/expanded-term matches, or
  - 1 strong domain marker from the hidden search profile.
- Do not require both intent and domain matches before AI. This avoids missing useful leads.
- Negative keywords are user-defined only in MVP. AI may suggest negative keywords later, but this is not part of MVP.

### Telegram bot notifications
- Notifications are sent immediately per found lead, not batched.
- MVP notification contains a short summary only.
- Later feature: add inline buttons to notification, for example "Open lead", "Hide", "Favorite".
- Later feature: "Open lead" should deep-link into the Mini App and focus the exact found lead/card.

### AI provider strategy
- MVP can start with an admin-provided Gemini API key.
- Future architecture should support an AI provider pool:
  - multiple provider adapters;
  - multiple configured API keys/accounts only where allowed by provider terms;
  - quota tracking;
  - automatic fallback from one provider/model to another;
  - no hard dependency on one AI vendor.
- Do not design the system around bypassing free-tier provider limits. Use paid quotas, local models, or compliant provider fallbacks for scale.

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

---

## Search MVP — Kwork Debug Findings From 2026-06-27

### Что проверили

Для Kwork были добавлены debug-логи:

- `debug/kwork-scans`
- `debug/kwork-candidates`
- `debug/search-ai-expansions`

По свежему scan-логу Kwork:

```text
ProjectsStatus: 200 OK
HasSession: True
IsConnected: True
RequiresReconnect: False
ProjectsHtmlContainsLoginPassword: False
ProjectsHtmlContains3183617: False
ProjectsHtmlContains3163214: False
ParsedProjectLinksCount: 0
CandidatesQueuedCount: 0
```

### Вывод

Kwork connection/session формально работает:

- сессия есть;
- Kwork не возвращает форму логина;
- `GET https://kwork.ru/projects` отвечает `200 OK`.

Но текущий `HttpClient`-scanner не получает реальные карточки заказов:

- в HTML нет ссылок вида `/projects/{id}/view`;
- в HTML нет тестовых заказов `3183617` и `3163214`;
- parser находит `0` project links;
- кандидаты не создаются;
- pre-filter не запускается;
- AI classifier не запускается;
- лиды не сохраняются;
- UI правильно показывает `0`, потому что backend реально не получил кандидатов.

То есть текущая проблема не в Gemini, не в фильтре и не во frontend-карточках. Проблема в источнике данных Kwork: обычный HTTP-запрос получает shell/общую страницу, а реальные заказы, вероятно, подгружаются через JavaScript/XHR после открытия страницы в браузере.

### Что нужно сделать дальше

Текущий Kwork scanner через `HttpClient` нужно заменить или дополнить browser-based scanner:

1. Использовать Playwright для Kwork scanning.
2. Открывать Kwork в браузерной сессии пользователя.
3. Использовать сохранённую login/session state.
4. Ждать загрузки страницы проектов.
5. Собирать ссылки заказов из DOM после JS-render.
6. При необходимости слушать network/XHR и найти API endpoint, который отдаёт заказы.
7. Для каждого заказа открывать detail page.
8. Читать не только title, но и полное описание заказа.
9. Передавать `title + description` в deterministic pre-filter.
10. Только кандидатов отправлять в Gemini classifier.
11. Сохранять confirmed leads в `JobLeads`.
12. После сохранения лид должен появляться в последних 10 заказах frontend.

### Важная техническая правка

Нельзя использовать `lastKworkHash` как marker времени. Hash не упорядочен по времени, поэтому часть заказов может пропускаться навсегда.

Правильный MVP-подход:

- dedup по `SourceId + ExternalId`;
- `ExternalId` для Kwork брать из URL: `kwork:{projectId}`;
- первый проход может обработать все видимые заказы;
- повторные проходы пропускают уже сохранённые `ExternalId`.

### Как диагностировать после следующей реализации

После внедрения browser-based scanner в `debug/kwork-scans` должно быть видно:

```text
BrowserScan: true
DomProjectLinksCount: N
NetworkProjectApi: <url если найден>
ProjectsHtmlContains3183617: true/false
ProjectsHtmlContains3163214: true/false
CandidatesQueuedCount: N
```

Если `DomProjectLinksCount > 0`, но лиды всё ещё не появляются, смотреть:

- `debug/kwork-candidates`;
- `PrefilterIsCandidate`;
- `PrefilterMatchedTerms`;
- `LEAD_SAVED` / `LEAD_NOT_SAVED`;
- AI confidence в `JobLeads`.

---

## Search MVP — Kwork Browser Scanner Implementation From 2026-06-28

Kwork scanner переведён с обычного `HttpClient GET /projects` на Playwright/browser-based scan.

Что реализовано:

- используется сохранённая Kwork session/cookies из `ExchangeConnections.EncryptedSession`;
- запускается headless Chromium через `Microsoft.Playwright`;
- создаётся browser context с Kwork cookies;
- открывается `https://kwork.ru/projects`;
- scanner ждёт DOM и JS/network activity;
- ссылки заказов читаются из уже отрендеренного DOM по `/projects/{id}/view`;
- detail page каждого заказа открывается в той же authenticated browser-сессии;
- читаются `h1`, `og:description`, meta description и `document.body.innerText`;
- в pre-filter отправляется `title + full rendered body/description`, а не только title;
- dedup идёт по `SourceId + ExternalId`;
- Kwork `ExternalId` берётся из URL как `kwork:{projectId}`.

Новые diagnostic-файлы:

- `debug/kwork-scans`
  - `BrowserScan: true`
  - `BrowserProjectsStatus`
  - `BrowserProjectsUrl`
  - `BrowserCookiesAdded`
  - `BrowserHtmlLength`
  - `BrowserBodyTextLength`
  - `BrowserBodyContains3183617`
  - `BrowserBodyContains3163214`
  - `DomProjectLinksCount`
  - `BrowserTotalProjectLinksCount`
  - `CandidatesQueuedCount`
- `debug/kwork-candidates`
  - `PROCESS_START`
  - `LEAD_SAVED` / `LEAD_NOT_SAVED`
  - pre-filter score
  - matched terms
  - content actually sent into ingestion

Если после browser scan всё ещё будет `DomProjectLinksCount: 0`, следующий шаг — смотреть browser network/XHR и найти внутренний endpoint Kwork, который отдаёт карточки заказов.

---

## Search MVP — Kwork All Rubrics Fix From 2026-06-28

Проблема после первого browser scanner:

- Kwork открывался с валидной сессией;
- scanner видел только любимые рубрики пользователя;
- в логах были в основном `Игры`, `Unity`, `GameDev`;
- нужные заказы из общих рубрик не попадали в скан;
- `element.click()` по тексту `Все` был недостаточен или падал при десериализации координат.

Исправление:

- scanner после открытия `https://kwork.ru/projects` ищет переключатель рубрик `Все`;
- клик выполняется доверенным Playwright mouse-click по координатам, а не synthetic `element.click()`;
- после клика scanner ждёт network idle и дополнительную паузу перед чтением DOM;
- DOM links фильтруются только как реальные project URLs `/projects/{id}` или `/projects/{id}/view`;
- ссылки покупателя `/projects/list/...` больше не попадают в candidates;
- стоп-слова в deterministic pre-filter теперь проверяются по границам слова, поэтому `бот` больше не матчится внутри `работоспособный`.

Подтверждение в diagnostics:

```text
RubricsAllClickResult: trusted_mouse_click
BrowserBodySnippet: ... Дизайн(87) Разработка и IT(135) Тексты и переводы(12) ...
ParsedLink: https://kwork.ru/projects/3206496 | title='Дизайн сайта'
PrefilterIsCandidate: True
PrefilterMatchedTerms: сайт, лендинг
Status: LEAD_SAVED
```

Текущее поведение:

- Kwork сканирует все рубрики, а не только любимые;
- scanner читает title + description/detail body;
- найденные candidates пишутся в `debug/kwork-candidates`;
- confirmed leads сохраняются в `JobLeads`;
- frontend последние 10 лидов должен обновлять через уже существующий polling.

## Search MVP — Kwork Access Block Handling From 2026-06-29

Kwork может временно блокировать автоматический доступ и переводить сессию на `/not_access.php`, если scanner слишком часто или слишком долго листает страницы.

Что изменено:

- при `KWORK_ACCESS_BLOCKED` поиск профиля автоматически останавливается (`SearchSettings.IsEnabled = false`);
- Kwork connection помечается как `RequiresReconnect`;
- пользователю отправляется Telegram bot notification о том, что поиск остановлен из-за антибот-проверки;
- scanner больше не листает все страницы Kwork одним burst;
- страницы 1, 2 и 3 проверяются каждый запуск, потому что там горячая зона свежих заказов;
- старые страницы проходятся маленькими порциями через cursor `kworkNextPage` в source credentials;
- это сохраняет покрытие всех страниц со временем, но резко снижает риск повторной блокировки.

Правка от 2026-06-29 после проверки логов:

- повторный клик по вкладке `Все` после перехода на `page=N` сбрасывал Kwork обратно на первую страницу;
- scanner теперь открывает страницы напрямую как `https://kwork.ru/projects?c=all&page=N` и не кликает `Все` повторно на каждой странице;
- Kwork list pre-filter больше не отбрасывает карточку, если есть хотя бы одно совпадение: detail page открывается, а финальное решение всё равно принимает общий pre-filter + AI classifier;
- общий pre-filter теперь пропускает один keyword match, если рядом есть buyer intent (`нужно`, `требуется`, `создать`, `разработать`, `доработать`, etc.).

## Search MVP — AI Classifier Precision Fix From 2026-06-29

Проблема:

- после расширения Kwork coverage в ленту попадали смежные, но нецелевые задачи;
- старый classifier prompt спрашивал слишком широко: “это платная задача вообще?”, а не “это задача соответствует скрытому профилю поиска пользователя?”;
- предыдущая формулировка ошибочно описывала отдельные домены через `если ...`, хотя ClientScout должен работать с любыми ключевыми словами и любым типом услуг;
- если Gemini возвращал null/error, код мог сохранить lead как keyword/error match.

Исправление:

- ClientScout search является полностью универсальным: пользователь может искать любые услуги, работы или заказы, не только IT;
- при сохранении keywords/negative keywords Gemini expansion строит скрытый профиль поиска в existing hidden arrays:
  - `ExpandedPositiveTerms`;
  - `ExpandedIntentTerms`;
  - `StrongTerms`;
  - normalized negative terms;
- скрытый профиль должен описывать смысл поиска: что пользователь ищет, какие формулировки могут использовать клиенты, какие синонимы/переводы относятся к этому домену, какие маркеры особенно сильные;
- classifier prompt теперь не содержит доменных `if`-правил и не привязан к сайтам, backend, Unity, дизайну или любой другой конкретной области;
- classifier должен реконструировать скрытый профиль поиска из keywords + expanded terms + strong terms + negative keywords, затем семантически сравнить с ним описание заказа;
- простое совпадение слова недостаточно: Gemini должен понять, совпадает ли реальная услуга/работа/результат заказа с тем, что пользователь искал;
- adjacent-but-different задачи должны отклоняться, даже если в тексте есть одно похожее слово;
- если Gemini доступен, но classification result is null/error, lead не сохраняется и сможет быть проверен повторно позже;
- 4 уже сохранённых false-positive Kwork лида были скрыты через `LeadStatus.Hidden`, чтобы не висели в UI, но dedup остался.

Рекомендация:

- для ручного теста допустимо 5 минут;
- для production минимальный интервал по Kwork лучше держать 15 минут;
- если аккаунт снова ловит антибот, поднять минимум до 30 минут и уменьшить количество старых страниц за один scan.

### Search MVP — Hidden Search Profile Upgrade From 2026-06-29

Решение:

- ClientScout search остаётся универсальным: пользователь может искать любые услуги или заказы, не только IT;
- при сохранении keywords/negative keywords Gemini теперь строит отдельный скрытый профиль поиска:
  - `SearchProfileSummary` — смысловое описание того, что пользователь ищет;
  - `MustIncludeSignals` — сильные признаки попадания в нужный домен;
  - `SoftSignals` — мягкие синонимы, переводы и связанные формулировки;
  - `RejectSignals` — смежные, но неподходящие темы, которые часто дают false-positive;
- deterministic pre-filter остаётся дешёвым:
  - сначала ищет user keywords + hidden signals + expanded terms;
  - учитывает buyer intent;
  - слабые совпадения с reject signals отбрасывает до AI;
- Gemini classifier получает `SearchProfileSummary` и сравнивает заказ с ним семантически, а не по одному слову;
- это должно уменьшить false-positive вроде SEO, публикаций на площадках или Unity/WebGL, если скрытый профиль поиска был про сайты/верстку;
- buyer intent расширен словами уровня “доделать”, “починить”, “пофиксить”, “реализовать”, “сверстать”, “настроить”, “integrate”, “configure”, “finish”, “debug”, etc.;
- AI всё ещё вызывается только:
  - при изменении keywords/negative keywords для expansion;
  - для candidates, прошедших deterministic pre-filter;
- каждое сырое сообщение/заказ в AI не отправляется.

### Search MVP — Gemini Quota Handling From 2026-06-29

Диагностика:

- Kwork scanner работал нормально: `debug/kwork-scans` показывал страницы, ссылки и candidates;
- хорошие Kwork candidates вроде “Доработать сайт”, “Верстка и интеграция сайта в WordPress”, “Создание 5 сайтов на Тильда” проходили deterministic pre-filter;
- лиды не сохранялись, потому что Gemini API вернул `429 RESOURCE_EXHAUSTED`;
- текущий бесплатный лимит для `gemini-2.5-flash-lite` был исчерпан (`GenerateRequestsPerDayPerProjectPerModel-FreeTier`);
- значит проблема была не в Kwork и не в БД, а в AI quota.

Исправление:

- `GeminiJsonClient` теперь отличает `QuotaExceeded` от других ошибок;
- если classifier получает 429/quota exceeded, candidate сохраняется как `AiUnavailable`;
- Telegram-уведомление для такого лида не отправляется;
- пользователь всё равно видит keyword-score кандидаты в UI и может вручную оценить качество pre-filter;
- если Gemini возвращает битый JSON или другую непонятную ошибку, candidate пока не сохраняется, чтобы не засорять список мусором.

## Search MVP — AI Logic Update & Kwork Parser Fix (Session Antigravity, 2026-06-29)

### Что было сделано и почему это важно:
В этой сессии (29 июня 2026) мы решили комплекс проблем, связанных со сканером Kwork и логикой AI-классификации, из-за которых новые заказы не отображались в интерфейсе. 

**Для Кодекса или следующего агента — внимательно изучи этот блок, здесь описана актуальная архитектура парсинга и обработки заказов.**

#### 1. Очистка входных данных парсера (Kwork)
* **Проблема:** Kwork-парсер (JavaScript-инъекция в SearchJobService.cs) отправлял нейросети весь контент карточки заказа, включая боковую панель (категории, статистику продавца, желаемый бюджет и т.д.). Этот "мусор" сбивал нейросеть с толку, и она отклоняла хорошие лиды, занижая уверенность.
* **Решение:** Мы обновили JS-код в SearchJobService.cs (ReadKworkItemsFromCurrentPageAsync), добавив удаление блоков с классом .m-project-view__info перед извлечением текста (el.remove()). Теперь нейросеть получает **исключительно заголовок и чистый текст описания задачи**.
* **Логи:** Из логов сканирования (папка debug/kwork-scans/) был удален BrowserBodySnippet (строка 513 в SearchJobService.cs), так как он создавал огромный визуальный шум, который путал разработчика при дебаггинге.

#### 2. Фикс блокировки лидов при отсутствии токенов AI (Изящная деградация)
* **Проблема:** Ранее в SearchIngestionService.cs стояло жесткое условие: если settings.NeedsAiExpansion == true или скрытый профиль пуст, метод ProcessCandidateAsync немедленно возвращал 
ull. Из-за этого, если у пользователя заканчивались токены Gemini (ошибка 429), скрытый профиль не генерировался, и **все последующие заказы полностью игнорировались**, даже не попадая в БД! Интерфейс при этом пустовал.
* **Решение:** Блокировка была убрана. Если профиль еще не расширен (NeedsAiExpansion = true), заказ всё равно проходит через базовый фильтр ключевых слов (Prefilter). Если ключевые слова совпали, заказ сохраняется в базу (JobLeads) со статусом AiStatus = KeywordOnly. Таким образом, при падении нейросети приложение не ломается, а переходит на резервный механизм работы — по ключевым словам.

#### 3. Генерализация и универсальность промпта AI
* **Проблема:** Пользователь указал, что приложение ClientScout является универсальным (можно искать заказы на дизайн, поклейку обоев, строительство и т.д.), хотя 90% пользователей — из IT. При отсутствии скрытого профиля нейросеть могла теряться или быть слишком IT-центричной.
* **Решение:** Промпт в AiLeadClassifier.cs был серьезно переработан.
    * Добавлено правило: *"Пользователь может искать любые услуги (IT, дизайн, маркетинг, строительство и т.д.). И хотя 90% пользователей — это IT, нейросеть должна строго следовать ключевым словам"*.
    * Добавлен fallback-сценарий: если SearchProfileSummary пуст, нейросеть должна использовать сырые массивы UserKeywords, ExpandedPositiveTerms, и StrongTerms как основной ориентир для классификации.
    * Нейросеть обучена оценивать **конечный результат работы (deliverable)**. Например, если слово "сайт" фигурирует в заказе "Сделать фото товаров с инфографикой для сайта", нейросеть поймет, что суть работы — картинки, а не программирование, и отсеет заказ (если пользователь ищет разработку). Первый фильтр пропустит этот заказ по слову "сайт", но AI успешно его заблокирует.

### Рекомендации Кодексу (Codex) на будущее:
1. **Не возвращай жесткие блокировки в SearchIngestionService.** Никогда не блокируй сохранение LeadStatus.New, если нейросеть недоступна, иначе интерфейс приложения будет казаться пользователю "мертвым".
2. При добавлении новых бирж (например, FL, Freelance.ru) применяй ту же логику парсинга — отправляй в AI только **суть задачи**, вырезая боковые панели, отзывы и бюджет.
3. Логика Fallback на KeywordOnly работает отлично, сохраняй этот подход при дальнейшей разработке AI-функционала.

