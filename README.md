# School Admission System

First-come-first-served online admission for a school. Every class shift is its own "class" with a
limited number of seats. A parent applies; the seat is **held** for a limited time; paying the
admission fee **confirms** it, otherwise the seat goes back to the pool by itself.

- **Backend:** ASP.NET Core (.NET 10) + PostgreSQL 17 (EF Core) in [`backend/`](backend/)
- **Frontend:** React 19 + TypeScript (Vite), served by nginx, in [`frontend/`](frontend/)
- **Browser tests:** Playwright in [`e2e/`](e2e/)

## Run with Docker

The only requirement is Docker (with Compose v2).

```bash
docker compose up --build
```

| What | URL |
|------|-----|
| **Frontend: open this** | **http://localhost:3000** |
| Backend API | http://localhost:8080 |
| Health check (database included) | http://localhost:8080/health |

```bash
docker compose down        # stop; the data is KEPT (named volume "pgdata")
docker compose down -v     # stop and DELETE the data
```

`docker compose down` followed by `docker compose up` keeps every class and application.
PostgreSQL is not published on a host port, so it cannot clash with a database already running on your machine.

### Configuration

All settings are environment variables; the defaults work out of the box.

| Variable | Default | Meaning |
|----------|---------|---------|
| `HOLD_DURATION_SECONDS` | `600` | How long a seat stays on hold. It is released automatically afterwards |
| `ConnectionStrings__Default` | set in `docker-compose.yml` | PostgreSQL connection string of the backend |
| `API_BASE_URL` | `http://localhost:8080` | Backend URL the **browser** calls (read when the frontend container starts, no rebuild) |
| `CORS_ALLOWED_ORIGINS` | `*` | Comma-separated list of allowed browser origins |
| `EXPIRY_SWEEP_INTERVAL_SECONDS` | `5` | How often the housekeeping job tidies expired holds (never affects correctness) |

Example: `HOLD_DURATION_SECONDS=60 docker compose up --build`

## Using the app

1. Type the student's **17-digit birth registration number** in the box at the top (Bangla digits are converted). It is remembered in the browser.
2. **Class তৈরি**: the school admin creates a class from a name and a seat count (one class per shift, e.g. "Class 6 - Morning Shift").
3. **Class তালিকা**: total, held, confirmed and available seats, refreshed every 3 seconds without a reload. **আবেদন করুন** holds a seat.
4. **আবেদন**: status, time left, **ভর্তি ফি দিন** (simulated payment, confirms the admission) and **বাতিল**. When the time runs out the page shows `EXPIRED` by itself and the pay button is disabled.
5. **আমার আবেদন**: all applications of the current student; a running one is shown first.

## Running the tests (Docker only)

```bash
# 166 backend tests: unit, integration (real PostgreSQL), concurrency and multi-process tests
docker compose -f docker-compose.test.yml run --rm --build backend-tests

# 106 frontend tests (Vitest, Testing Library, MSW)
docker compose -f docker-compose.test.yml run --rm --build frontend-tests

docker compose -f docker-compose.test.yml down -v     # remove the throwaway test database
```

The backend tests include the **concurrency tests** (`ConcurrencyTests`, `MultiInstanceConcurrencyTests`): hundreds of
simultaneous requests, including from several API processes, must never hold more seats than exist.

**End-to-end tests** drive the real UI in a browser against the running stack. Start the stack with a short hold
so the expiry test is quick, then run Playwright in its own container:

```bash
HOLD_DURATION_SECONDS=15 docker compose up --build -d
docker run --rm --network host -u "$(id -u):$(id -g)" -e HOME=/tmp -v "$PWD/e2e":/e2e -w /e2e \
  mcr.microsoft.com/playwright:v1.65.0-noble npx playwright test
```

## Two backend processes (bonus)

```bash
docker compose --profile bonus up --build     # second backend on http://localhost:8081, same database
```

Seats stay correct across processes: see [ASSUMPTIONS.md](ASSUMPTIONS.md).

## API

Base URL `http://localhost:8080`, JSON. The student is identified by the `X-Student-Id` header (17 digits).

| Method and path | Purpose | Success |
|-----------------|---------|---------|
| `POST /classes` | Create a class `{ "name", "totalSeats" }` | `201` |
| `GET /classes` | All classes with `heldSeats`, `confirmedSeats`, `availableSeats` | `200` |
| `GET /classes/{classId}` | One class | `200` |
| `POST /classes/{classId}/holds` | Apply (header `X-Student-Id`) | `201`, `409` if no seat |
| `GET /holds/{holdId}` | Current state: `HELD`, `CONFIRMED`, `EXPIRED` or `CANCELLED` | `200` |
| `POST /holds/{holdId}/confirm` | Pay the fee and confirm (header) | `200` |
| `DELETE /holds/{holdId}` | Cancel (header) | `204` |
| `GET /me/holds` | All applications of the student (header) | `200` |
| `GET /health` | Liveness and database check (operational, not a feature) | `200` |

Errors are `application/problem+json` with a machine-readable `code`, for example:

```json
{ "type": "about:blank", "title": "No seats available", "status": 409,
  "detail": "Class 'Class 6 - Morning Shift' has no available seats.", "code": "NO_SEATS_AVAILABLE" }
```

Codes: `VALIDATION_ERROR`, `STUDENT_ID_REQUIRED`, `INVALID_STUDENT_ID` (400); `NOT_HOLD_OWNER` (403);
`CLASS_NOT_FOUND`, `HOLD_NOT_FOUND`, `NOT_FOUND` (404); `NO_SEATS_AVAILABLE`, `ACTIVE_HOLD_EXISTS`,
`ALREADY_CONFIRMED`, `HOLD_EXPIRED`, `HOLD_CANCELLED`, `CANNOT_CANCEL_CONFIRMED` (409).

## Project structure

```
backend/    ASP.NET Core API (Http endpoints, Services with the rules, Data = EF Core + migrations) and its xUnit tests
frontend/   React app (api client, hooks, pages, components) with Vitest tests, Dockerfile, nginx config
e2e/        Playwright browser tests
docker-compose.yml        the application (db, backend, frontend, optional second backend)
docker-compose.test.yml   the automated tests, Docker only
```

## Changelog

- v1.0.0: first release
