<p align="center">
  <img src="docs/images/banner.png" alt="Milo — Show up. Log it. Beat it." width="100%">
</p>

<p align="center">
  A self-hosted <b>training, meals and weigh-in tracker</b> that turns your weekly plan into a day you tick off,<br>
  with a <b>day score</b>, a <b>streak</b>, a <b>session mode</b> for the gym floor and records that feel like records.
</p>

<p align="center">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white">
  <img alt="SQL Server" src="https://img.shields.io/badge/SQL%20Server-Dapper%20%2B%20stored%20procedures-CC2927?logo=microsoftsqlserver&logoColor=white">
  <img alt="PWA" src="https://img.shields.io/badge/PWA-installs%20on%20the%20phone-3355FF">
  <img alt="Docker" src="https://img.shields.io/badge/Docker-UGREEN%20NAS-2496ED?logo=docker&logoColor=white">
  <img alt="CI" src="https://github.com/rodrigocoelho2/Milo/actions/workflows/ci.yml/badge.svg?branch=main">
</p>

<p align="center">
  <a href="#features">Features</a> ·
  <a href="#screenshots">Screenshots</a> ·
  <a href="#quick-start-docker-on-the-nas">Quick start</a> ·
  <a href="#moving-from-corpoops">From CorpoOps</a> ·
  <a href="#development">Development</a> ·
  <a href="#api">API</a>
</p>

<p align="center">
  <img src="docs/images/hero.png" alt="Milo: Today on the computer and on the phone" width="100%">
</p>

> Milo used to be called BodyOps, and before that CorpoOps (a single-file app). Old backups import as they are.

---

## Features

**Every day**

- **Today is a timeline**: the weigh-in, each meal at its planned time and the workout, in the order you live them.
  The next thing is highlighted and one tap ticks it off.
- **Day score and streak**: each day scores out of 100 (training 50, meals 35, weigh-in 15) and fills as you go.
  Days of 60 or more in a row make the streak, shown on Today, the side bar and the week.
- **Quick log**: the **+** button logs a weigh-in, ticks the next meal or starts the workout from any screen.

**At the gym**

- **Session mode**: the workout on the whole screen, one exercise at a time. Big load buttons in steps of 2.5 kg,
  last time's load and reps to beat, a tile per set, and the rest timer starts by itself after each set
  (+30 s or skip; its length is in Settings). It survives locking the phone and keeps the screen awake.
- **Records**: lift more than ever before and the record sheet comes up with confetti: how much more than your best,
  how far you have come since you started, and which record of the month it is.

**The week and the long run**

- **Week**: a stacked bar per day (training, meals, weigh-in), sessions, meals, volume lifted against last week,
  records, the sets each muscle got on a body map and a recap. Tap a day to fix its meals or session.
  Reset week has Undo.
- **Progress**: body weight against your goal, with the pace ("90 kg is roughly 9 months away"), every lift's best
  load with its trend line, and the list of records.

**Your plan**

- Each weekday gets a **name** ("Upper body", "Legs"), **exercises** (sets × reps, notes) and **meals** (a time,
  foods in grams or pieces). Copy a day to others, drag to reorder; changes save themselves.
- The **muscles** of each exercise are detected from its name (English or Portuguese) or picked by hand.
- Editing the plan updates this week and the weeks ahead; past weeks keep the plan they had.

**Built to self-host**

- One `docker-compose.yml`: **SQL Server 2022 Express**, a one-shot **database deploy** (tables and stored procedures
  from a dacpac, applied again on every update) and the **app**.
- **Accounts**: everyone has their own plan and history; the first account is the admin, who can reset a forgotten
  password. Sign-ups can be closed.
- **Installs as an app** on iPhone and Android, with an offline page; light and dark themes follow the device.
- **Nothing from the internet at runtime**: the Geist font and icons ship with the app.

## Screenshots

### At the gym
<img src="docs/images/train.png" alt="Phone: Today, session mode with the rest timer, a new personal record and the quick log">

### Week, progress and plan
<img src="docs/images/track.png" alt="Phone: the week, progress with weight and lifts, and the plan's training and meals">

### On the computer
<table>
<tr>
<td width="50%"><b>Week</b><br><img src="docs/images/desktop-week.png" alt="Week: stacked day bars, totals, the recap, muscles and day by day"></td>
<td width="50%"><b>Progress</b><br><img src="docs/images/desktop-progress.png" alt="Progress: weight against the goal, weigh-ins and every lift's best"></td>
</tr>
<tr>
<td width="50%"><b>Plan</b><br><img src="docs/images/desktop-plan.png" alt="Plan: named days, exercises with muscles, and what the day works"></td>
<td width="50%"><b>Settings</b><br><img src="docs/images/desktop-settings.png" alt="Settings: account, rest timer, theme, backups and users"></td>
</tr>
</table>

### Light and dark
<img src="docs/images/themes.png" alt="Today in the light and dark themes">

### Welcome
<img src="docs/images/welcome.png" alt="The landing page on the computer and the phone, and creating an account">

<details>
<summary>Design mocks</summary>

The approved HTML mocks of the interface are in [`docs/mocks`](docs/mocks): the app (`bodyops-daylight.html`) and
the landing page with sign-in (`bodyops-welcome.html`). Open them in a browser.
</details>

## Quick start (Docker on the NAS)

```bash
git clone https://github.com/rodrigocoelho2/Milo.git && cd Milo
cp .env.example .env          # then set SQL_SA_PASSWORD
docker network create nas-net # once, if it doesn't exist yet
docker compose up -d --build
```

Three containers start: **bodyops-sql** (SQL Server 2022 Express), **bodyops-db-deploy** (publishes
`BodyOps.Database` — creates the database the first time, applies schema changes on later deploys — and exits)
and **bodyops** (the app). Open `http://<nas-ip>:8086` and create the first account: it becomes the admin.

> The containers, volumes, database and code still use the old name **BodyOps**, so an update never loses data.

| Setting (`.env`) | Default | What it does |
| --- | --- | --- |
| `SQL_SA_PASSWORD` | — | SQL Server `sa` password (8+ characters, three of upper, lower, digits, symbols). |
| `SQL_PORT` | `1434` | Host port for SSMS / Azure Data Studio (`<nas-ip>,1434`, user `sa`). |
| `SQL_MEMORY_LIMIT_MB` | `1024` | Keeps SQL Server from taking most of the NAS memory. |
| `BODYOPS_PORT` | `8086` | Host port of the app. |
| `BODYOPS_ALLOW_REGISTRATION` | `true` | `false` once everyone has an account (the first account can always be created). |
| `BODYOPS_BEHIND_PROXY` | `false` | `true` when Nginx Proxy Manager sits in front (trusts `X-Forwarded-*`). |

**Nginx Proxy Manager:** the app joins `nas-net`, so a proxy host can forward to `http://bodyops:8080`
(e.g. `bodyops.prometheus-nas.com`, with the wildcard certificate). Set `BODYOPS_BEHIND_PROXY=true`.

**Updating:** `git pull && docker compose up -d --build`. The database deploy runs again and only applies what changed.

**Backups:** the data lives in the `bodyops-sql` volume. Settings › Export backup downloads one account as JSON;
for the whole server, back up the database (`BACKUP DATABASE BodyOps TO DISK = …` from SSMS) or the volume.

**Already running SQL Server for HOME Living Dex?** Milo can share it: drop the `sqlserver` service, point
`db-deploy`'s `SQL_SERVER` and the app's connection string at `livingdex-sql`, and put both stacks on one network.
The database is called `BodyOps`, so it never touches `LivingDex`.

## Moving from CorpoOps

1. In the old app, **Exportar backup** (or use the English copy of your data, `corpoops-backup-english.json`).
2. In Milo, create your account, then **Settings › Import backup** and pick the file.

The import reads CorpoOps' weekdays (`Seg` … `Dom`), meals with grams or units, exercises and hand-picked muscles,
every logged week with the plan snapshot it had, the `"3x10"` text of each exercise as three sets of 10, cardio
minutes and kilometres, the weigh-ins and the weight goal. Importing replaces the account's data and offers Undo.

## Development

### Solution layout

```
BodyOps.slnx
├── BodyOps.Database/          SQL Server project (SDK-style .sqlproj → dacpac)
│   ├── Tables/                User, UserSetting, PlanDay, PlanMeal, PlanFood, PlanExercise, Day, DayMeal, DayExercise, DaySet, WeightEntry
│   ├── Views/                 PlanMealSummary (a meal's foods on one line)
│   ├── StoredProcedures/      Users, Settings, Plans, Days, Weights — the only way the app touches data
│   └── Scripts/               post-deployment script and deploy.sh (used by the db-deploy container)
├── BodyOps.Library/           enums, ServiceApiResponse, validation types, header names (no ASP.NET)
├── BodyOps.WebApi/            ASP.NET Core API + the page
│   ├── Extensions/            authentication (cookie, PBKDF2), data (Dapper over stored procedures), JSON, time, messages
│   ├── Features/              Accounts, Plans, Days, Exercises, Weights, Settings, Backups, Health
│   │   └── <Feature>/         Controllers, Models (Entities, DTOs), Repositories, Services
│   └── wwwroot/               index.html, css/app.css, js/ (one file per screen), fonts, icons, manifest, service worker
├── BodyOps.UnitTest/          xUnit: services and rules with in-memory fakes, plus SQL Server integration tests
└── docs/                      README images and the HTML mocks of the interface
```

Controllers only map HTTP to services; services hold the rules and answer with `ServiceApiResponse<T>`;
repositories only call stored procedures. All user-facing text is English.

### How logged days work

A day gets a row in `dbo.Day` the first time something is ticked or logged on it, with a copy of that weekday's
plan in `dbo.DayMeal` and `dbo.DayExercise` (sets in `dbo.DaySet`). Editing the plan calls `dbo.DaySyncFromPlan`
for days from this week's Monday on, so the current week follows the plan while earlier weeks keep what they had.
Days with nothing logged show the current plan. An exercise is matched across days by its name (ignoring case and
spaces) for last time, personal records and progression.

### Running it on the PC

- Visual Studio (or `dotnet`) with the .NET 10 SDK, and a SQL Server (Docker: `docker compose up -d sqlserver db-deploy`).
- `BodyOps.WebApi/appsettings.Development.json` has the connection string (`Server=localhost,1433`, adjust the port/password).
- Run **BodyOps.WebApi** → `http://localhost:5086` (Swagger at `/swagger` in Development).

### Tests

```bash
dotnet test BodyOps.UnitTest
# SQL Server integration tests too (each test builds and drops its own database):
BODYOPS_TEST_SQL="Server=localhost,1433;User Id=sa;Password=…;TrustServerCertificate=True" dotnet test BodyOps.UnitTest
```

GitHub Actions (`.github/workflows/ci.yml`) builds everything, publishes the dacpac twice (the second publish must
change nothing), runs every test against SQL Server, then starts the Compose stack, smoke-tests it and takes
screenshots. Logs and screenshots are pushed to the `ci-output-build` and `ci-output-compose` branches.

### API

All under `/api`, JSON, cookie sign-in. Writes need the `X-BodyOps: 1` header.

| | |
| --- | --- |
| `GET health` | first run, sign-up open |
| `POST auth/register` · `auth/login` · `auth/logout` · `auth/password` · `auth/delete-account`, `GET auth/me` | account |
| `GET admin/users`, `POST admin/users/{id}/reset-password`, `DELETE admin/users/{id}` | admin only |
| `GET plan`, `PUT plan/{weekday}/meals`, `PUT plan/{weekday}/exercises`, `PUT plan/{weekday}/name`, `POST plan/{weekday}/copy` | weekday 1 = Monday |
| `GET days/week?date=`, `GET days/streak`, `GET days/consistency?weeks=` | weeks (with each day's score), the streak |
| `PUT days/{date}/meals/{planMealId}`, `PUT days/{date}/meals`, `PUT days/{date}/training`, `PUT days/{date}/exercises/{planExerciseId}` | logging (answers with the week) |
| `DELETE days/week/{date}`, `POST days/week/{date}/restore` | reset week and undo |
| `GET exercises`, `GET exercises/history?name=`, `GET exercises/records?limit=` | lifts, progression, records |
| `GET weights`, `PUT weights/{date}`, `DELETE weights/{date}`, `GET`/`PUT settings` | weight, goal and rest between sets |
| `GET backup`, `POST backup` | export, import (Milo/BodyOps or CorpoOps) |

## Licences

Font: Geist, SIL Open Font License (`BodyOps.WebApi/wwwroot/fonts`).

<br>
<p align="center"><sub><i>Milo of Croton lifted a newborn calf onto his shoulders and carried it every day. The calf grew, and so did he.</i></sub></p>
