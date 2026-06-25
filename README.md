# RentalAI — Backend

Property rental platform with AI-powered identity verification, intelligent chatbot, and owner performance dashboard.

Built on **.NET 10** as a modular monolith, with **Docker Compose** to spin up the entire environment with a single command.

---
https://github.com/Jaramc/rental-ai-backend
https://github.com/Jaramc/rental-ai-frontend
## Live Demo

| Service | URL |
|---------|-----|
| **Frontend** | [https://rentalai.jaramc.dev](https://rentalai.jaramc.dev) |
| **API** | [https://api.rentalai.jaramc.dev](https://api.rentalai.jaramc.dev) |
| **API Docs (Swagger)** | [https://api.rentalai.jaramc.dev/swagger](https://api.rentalai.jaramc.dev/swagger) |

---

## Repositories

| Repo | Description |
|------|-------------|
| **rental-ai-backend** *(this repo)* | Modular monolith .NET 10, infrastructure |
| [rental-ai-frontend](https://github.com/Jaramc/rental-ai-frontend) | Next.js 14 application |

---

## Prerequisites

| Tool | Minimum version | Check |
|------|----------------|-------|
| Docker Desktop / Docker Engine | 24.x | `docker --version` |
| Docker Compose | 2.x (plugin) | `docker compose version` |
| Git | 2.x | `git --version` |
| Available RAM | 6 GB | — |

> The project **does not require** .NET, PHP, or Node installed locally. Everything runs inside Docker containers.

---

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/Jaramc/rental-ai-backend.git
cd rental-ai-backend
git checkout feature/infra-skeleton
```

### 2. Configure environment variables

```bash
cp .env.example .env
```

Open `.env` and replace these values:

```env
OPENAI_API_KEY=sk-proj-YOUR_REAL_KEY
MAIL_USERNAME=your_mailtrap_user
MAIL_PASSWORD=your_mailtrap_password
```

> Encryption keys (`JWT_SECRET`, `KYC_ENCRYPTION_KEY`) ship with dev-only defaults in `.env.example`. **Never use those in production.**

### 3. Start all services

```bash
docker compose up -d --build
```

First run downloads base images (~3 min). Database migrations run automatically on startup.

### 4. Verify everything is running

```bash
docker compose ps
```

All services should show `healthy` status.

---

## Available URLs (local development)

| Service | URL | Credentials |
|---------|-----|-------------|
| **API** | http://localhost:5000 | — |
| **Swagger / Scalar** | http://localhost:5000/scalar | — |
| **phpMyAdmin** | http://localhost:5050 | `MYSQL_*` from `.env` |
| **RabbitMQ Management** | http://localhost:15672 | `RABBITMQ_*` from `.env` |
| **MinIO Console** | http://localhost:9001 | `MINIO_*` from `.env` |
| **Seq (logs)** | http://localhost:5341 | — |
| **Qdrant Dashboard** | http://localhost:6333/dashboard | — |
| **Hangfire Dashboard** | http://localhost:5000/hangfire | `HANGFIRE_*` from `.env` |

---

## Architecture

### Why a Modular Monolith

The backend is a single ASP.NET Core application organized into modules by folder (Auth, Properties, Booking, KYC, Users, Dashboard, Files, Notifications). For the scope of this assessment — a single author with no need to scale parts independently — a modular monolith delivers the same features with far less operational complexity than microservices. Modules communicate in-process; boundaries are kept clean so any module could be extracted into its own service later if load ever requires it.

### Key Technical Decisions

#### Double-Booking Prevention (two layers)

Availability is validated in two layers. First, a **Redis distributed lock** (`lock:property:{id}:{checkIn}:{checkOut}`, TTL 30s) blocks the resource during the transaction to prevent race conditions from concurrent requests. Second, the insert runs inside a **MySQL transaction** that locks the property row with `SELECT ... FOR UPDATE`, checks for date overlap (`CheckIn < @newCheckOut AND CheckOut > @newCheckIn`), and inserts only if no conflict is found. MySQL lacks partial unique indexes and exclusion constraints, so the overlap check lives in the transaction, not the engine. The Redis lock is the primary barrier; the transaction guarantees integrity even if Redis were to fail.

#### Standardized Check-in / Check-out

Every confirmed booking automatically sets check-in to **14:00** and check-out to **12:00** in the property's local time, applied server-side. Users cannot change these times, ensuring a consistent policy across the platform.

#### Omnichannel Notifications

The application sends notification emails (via **MailKit** + SMTP) and creates in-app notifications in fire-and-forget mode so the HTTP response is never blocked. Events: booking confirmation (to guest + owner), KYC verdict (approved/rejected), and check-in/check-out reminders. Templates are branded HTML with the RentalAI identity.

#### Secure KYC

Identity documents are encrypted with **AES-256-GCM** before being stored in MinIO, with a per-user key derived using HKDF from `KYC_ENCRYPTION_KEY` + userId. A **Hangfire** background job deletes the document after `KYC_DOCUMENT_TTL_HOURS` hours (byte overwrite + logical deletion). The AI-extracted text is stored hashed; only the final verdict (approved/rejected) persists in cleartext.

#### AI-Powered Document Verification

The KYC module sends the uploaded document to **OpenAI GPT-4o Vision** with a structured prompt to extract: full name, document number, and date of birth. Fields are validated and a verdict (Approved/Rejected) is emitted automatically.

#### Deferred Authentication

The property catalog is fully public (no JWT required). The wishlist is maintained in a signed cookie for anonymous users. Login is requested only when the user attempts to confirm a booking or persist favorites permanently; on login, anonymous favorites are automatically merged into the registered profile.

### Request Map

```
Client (Next.js)
    │
    ▼
RentalAI.Api :5000 (ASP.NET Core)
    │
    ├── /auth/*           → Auth module
    ├── /properties/*     → Properties module
    ├── /bookings/*       → Booking module
    ├── /kyc/*            → KYC module
    ├── /users/*          → Users module
    ├── /dashboard/*      → Dashboard module
    ├── /files/*          → Files module
    └── /notifications/*  → Notifications module
```

### Full Stack

| Layer | Technology |
|-------|-----------|
| Core application | .NET 10 / ASP.NET Core (modular monolith) |
| ORM | Entity Framework Core 9 (Pomelo MySQL) |
| Database | MySQL 8.4 (LTS) |
| Cache / Locks | Redis 7 |
| Object storage | MinIO (S3-compatible) |
| Background jobs | Hangfire (MySQL storage) |
| Email | MailKit → SMTP (Mailtrap in dev) |
| AI — OCR | OpenAI GPT-4o Vision |
| Logging | Serilog → Seq |
| Vector DB | Qdrant (for chatbot RAG) |
| Chatbot | n8n workflow + GPT-4o-mini |
| Validation | FluentValidation |

---

## API Endpoints

### Auth
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/auth/register` | — | Register (Guest or Owner) |
| POST | `/auth/login` | — | Login, returns JWT + refresh token |
| POST | `/auth/refresh` | — | Rotate refresh token |
| POST | `/auth/logout` | JWT | Revoke refresh token |
| GET | `/auth/me` | JWT | Current user profile |

### Properties
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/properties` | — | Search with filters (city, dates, price, guests) |
| GET | `/properties/{id}` | — | Property detail with photos |
| POST | `/properties` | Owner | Create property |
| PUT | `/properties/{id}` | Owner | Update (own property only) |
| DELETE | `/properties/{id}` | Owner | Soft delete |
| POST | `/properties/{id}/photos` | Owner | Upload photo (max 10, max 5MB, jpg/png/webp) |
| DELETE | `/properties/{id}/photos/{photoId}` | Owner | Delete photo |

### Bookings
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/bookings` | JWT | Create booking (requires KYC approved) |
| GET | `/bookings` | JWT | My bookings |
| GET | `/bookings/{id}` | JWT | Booking detail |
| POST | `/bookings/{id}/cancel` | JWT | Cancel booking |

### KYC
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/kyc/verify` | JWT | Upload document + AI verification |
| GET | `/kyc/status` | JWT | Current KYC status |

### Users
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/users/wishlist` | Mixed | List wishlist (cookie or JWT) |
| POST | `/users/wishlist/{propertyId}` | Mixed | Add to wishlist |
| DELETE | `/users/wishlist/{propertyId}` | Mixed | Remove from wishlist |
| GET | `/users/profile` | JWT | User profile + KYC status |

### Dashboard (Owner only)
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/dashboard/summary` | Owner | KPIs: occupancy, revenue, properties |
| GET | `/dashboard/export` | Owner | Download Excel report (.xlsx) |

### Notifications
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/notifications` | JWT | My notifications |
| POST | `/notifications/{id}/read` | JWT | Mark as read |
| GET | `/notifications/unread-count` | JWT | Unread count |

### Health
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health/live` | Liveness probe |
| GET | `/health/ready` | Readiness probe (checks DB) |

---

## Security

- **Input validation**: FluentValidation on every DTO; unknown fields rejected.
- **Double-booking**: Redis distributed lock (TTL 30s) + MySQL `SELECT FOR UPDATE` with overlap check.
- **KYC encryption**: AES-256-GCM with per-user HKDF-derived key; document deleted after 24h.
- **Authentication**: Short-lived JWT (15 min) + hashed refresh tokens with rotation.
- **Rate limiting**: Global 100 req/min + strict 10 req/min on auth and KYC endpoints.
- **Least privilege**: App connects to MySQL as `rentalai` user, never root.
- **Correlation ID**: Propagated through every request via `X-Correlation-ID` header.
- **No secrets in repo**: Everything from environment variables.

---

## Repository Structure

```
rental-ai-backend/
├── src/
│   ├── RentalAI.Api/                 # Modular monolith (.NET 10)
│   │   ├── Modules/
│   │   │   ├── Auth/                 # JWT, registration, login
│   │   │   ├── Properties/           # CRUD, search, photos
│   │   │   ├── Booking/              # Reservations, double-booking prevention
│   │   │   ├── Kyc/                  # AI verification, encryption
│   │   │   ├── Users/                # Profile, wishlist
│   │   │   ├── Dashboard/            # KPIs, Excel export
│   │   │   ├── Files/                # MinIO storage
│   │   │   └── Notifications/        # Email + in-app
│   │   ├── Data/                     # DbContext, migrations
│   │   ├── Hosting/                  # App startup extensions
│   │   └── Dockerfile
│   └── shared/
│       ├── RentalAI.Common/          # Middleware, logging, health
│       └── RentalAI.Contracts/       # Shared contracts
├── docker/
│   └── mysql/init/                   # DB init script
├── docker-compose.yml
├── .env.example
├── CLAUDE.md                         # Development guidelines
└── README.md
```

---

## Useful Commands

```bash
# Real-time logs for a specific service
docker compose logs -f api

# All logs
docker compose logs -f

# Stop without deleting data
docker compose stop

# Stop and remove containers (keeps volumes)
docker compose down

# Full reset (DELETES all data)
docker compose down -v

# Rebuild API after code changes
docker compose up -d --build api

# Run locally (faster iteration)
# Keep infra in Docker, run API on host:
dotnet run --project src/RentalAI.Api
```

---

## Email Notifications

Transactional emails are sent via SMTP using Mailtrap in development. Emails are captured in the [Mailtrap inbox](https://mailtrap.io) — they never reach real inboxes. To view sent emails, log in to Mailtrap and open the sandbox inbox.

Events that trigger notifications:
- **Booking confirmed** → email + in-app to guest and owner
- **KYC approved/rejected** → email + in-app to user
- **Check-in/check-out reminders** → templates ready, job scheduling pending

---

## Known Limitations

- **Pomelo**: No stable release for EF Core 10 yet. Using EF Core 9 + Pomelo 9 on a `net10.0` project. Upgrade when Pomelo 10.x ships.
- **OpenAI KYC**: Requires a valid `OPENAI_API_KEY`. With a placeholder key, verification returns `Rejected` (correct behavior on failure). Mock to `Approved` via DB for testing.
- **Hangfire**: `Hangfire.MySqlStorage` pulls vulnerable transitive dependencies; overridden with `Newtonsoft.Json 13` and `Dapper 2.x` to reach 0 build warnings.

---

## License

[MIT](LICENSE)