# CLAUDE.md — rental-ai-backend

Read this before generating any code. These rules are not optional.

## Project

RentalAI is a property rental platform built as a technical assessment.
The backend is a set of .NET 10 microservices behind a YARP API gateway,
plus a Laravel notification worker. The frontend (Next.js 14) lives in a
separate repository: `rental-ai-frontend`.

Core features:
- Property catalog with photos and pricing
- Booking with strict anti double-booking guarantees
- KYC document validation using AI vision
- AI chatbot with RAG over the catalog
- Owner dashboard with KPIs and Excel export
- Deferred authentication (anonymous wishlist migrated to the profile on login)

## Tech stack

- .NET 10 (LTS), ASP.NET Core Web API, Entity Framework Core 10
- MassTransit over RabbitMQ for asynchronous messaging
- YARP as the API gateway
- MySQL 8.4, a single shared database
- Redis 7 for cache, distributed locks, and sessions
- MinIO for S3-compatible object storage
- Qdrant for vector search behind the chatbot RAG
- Hangfire for scheduled background jobs (for example, KYC document deletion)
- Serilog with Seq for structured logging
- FluentValidation for input validation
- Laravel 11 worker for notifications (email, push, in-app)

## Coding standards (non-negotiable)

1. All code and identifiers in English.
2. No comments. Code must be self-documenting through clear names and small, single-purpose functions.
3. Simple, conventional names. A reviewer should understand a name on first read. No invented or verbose names. Prefer `BookingService`, `CreateBooking`, `availableFrom`.
4. No unnecessary patterns. Add a pattern only when it removes real duplication or a real risk. Never add abstraction layers just in case.
5. Stay DRY. Shared DTOs and events live in `RentalAI.Contracts`. Shared middleware, logging, and health checks live in `RentalAI.Common`. Never copy logic between services.
6. Security is a factor in every decision (see below).

## Naming conventions

- Classes, methods, properties: PascalCase.
- Local variables and parameters: camelCase.
- Async methods end in `Async`.
- One responsibility per class. If a service file grows past a few hundred lines, split it.

## Security requirements

These hold everywhere and are graded:

- Validate every input with FluentValidation before it reaches domain logic. Reject unknown fields.
- Booking: prevent double-booking with a Redis distributed lock keyed `lock:property:{id}:{from}:{to}`, TTL 30s, backed by a database constraint as a second line of defense. Return 409 Conflict when the lock cannot be acquired.
- KYC: encrypt documents with AES-256-GCM before writing to MinIO, derive the key per user, never store the document unencrypted, and delete it within 24h via a Hangfire background job. Never log document contents.
- Auth: short-lived JWT access tokens plus refresh tokens. Never put secrets or PII in a token.
- Rate limiting on every public endpoint to resist abuse and takedown attempts. Use stricter limits on auth, KYC, and chatbot endpoints.
- Always use parameterized queries through EF Core. Never build SQL by string concatenation.
- Propagate a correlation ID across all services.
- Expose health checks at `/health/live` and `/health/ready` on every service.
- No secrets in code or in the repo. Everything comes from environment variables.

## Architecture

- One responsibility per service: Auth, Properties, Booking, KYC, Users, Dashboard, Chatbot, Files.
- Services communicate asynchronously over RabbitMQ. Avoid synchronous service-to-service HTTP unless a request truly needs an immediate answer.
- There is no payment service in scope. On a successful reservation, Booking publishes an integration event and the notification worker consumes it. Do not add a saga; only introduce one if payments later enter scope and a multi-step transaction genuinely needs compensation.
- Business logic lives in domain services or command handlers, never in controllers.

## Domain rules

- Catalog browsing and filtering by location and date range are public; no login required. Login is requested only to confirm a booking, to pay, or to persist the wishlist permanently.
- Every confirmed booking sets check-in to 14:00 and check-out to 12:00 in the property's local time, applied server-side. Users cannot change these times.
- A user must pass KYC (approved verdict) before completing their first booking.
- A booking carries a price (nightly rate times nights). There is no payment gateway in scope; "price paid" in the Excel report is that booking price.
- The anonymous wishlist lives in a signed cookie and is merged into the user profile on login.

## Git workflow

- `main` is production. `develop` is the integration branch.
- Branch off `develop` for every feature, then open a pull request back into `develop`.
- Branch names: lowercase and conventional (`feature/booking-locks`, `fix/kyc-ttl`). No capitalized or invented names.
- Write commit messages in English, short and in the imperative ("Add booking lock", not "Added the booking locks").
- Never commit `.env` or any secret. Verify with `git ls-files` that no secret file is tracked.

## Do not

- Do not add comments.
- Do not introduce design patterns that the code in front of you does not justify.
- Do not duplicate logic across services.
- Do not weaken any security control to make a feature easier.
- Do not invent unusual names.