# AI usage

**Tool:** Claude Code (Anthropic Claude): Opus 5.5 for the architecture and plan, Sonnet 5.5 for most of the implementation.

## Where it was used
- Architecture, requirement breakdown and the commit plan.
- Writing the backend (.NET), the frontend (React), the SQL, the Dockerfiles and compose files.
- Writing the tests (unit, integration, concurrency, end to end) and these documents.

Everything was built and run through Docker; I ran every test suite and the full stack myself.

## Where the AI was wrong, and how it was caught
- **Connection pool.** The design ignored pool sizing: Npgsql's default pool (100) equals PostgreSQL's default `max_connections` (100). With 200 simultaneous applications, 60 to 90 requests failed with HTTP 500 ("too many clients already"). Seats were never oversold, but requests were lost. The concurrency test caught it; the pool is now capped at 30 per process (separate `fix` commit).
- **Wrong test-library option.** It used `onUnhandledRequest` for MSW, which was renamed in the installed MSW 3 (`onUnhandledFrame`); the old name was silently ignored, so missing mocks would only warn. The TypeScript build caught it.
- **Stale countdown.** For a moment after new data arrived, the countdown used a clock reading older than the data and showed a time slightly too high. Two component tests failed; "now" is now never earlier than the moment the data was received.
- **Unrealistic mocks.** Two page tests mocked the server as unchanging after paying or cancelling, so the page (correctly) refetched and went back to HELD. The tests, not the page, were wrong; the mocks now change state like the server.
- **Tests sharing data.** Several tests listed one student's applications while other tests in the same class had used the same student number, so they failed; those tests now use their own student numbers.
- **Tooling assumptions.** The plan assumed a `.sln` file and a `.config/` tool manifest location; the .NET 10 SDK creates `.slnx` and the manifest elsewhere, which would have broken the Dockerfile. Found while building the image.
- **Noise that looked like errors.** The container printed a Kerberos library error and a "failed" migration query on the first start. Both were harmless but confusing, and were removed.

## What I verified myself
- All backend, frontend and browser tests, repeated several times (the concurrency tests also against a PostgreSQL with default settings).
- `docker compose up` from scratch, data surviving `down` and `up`, and 30 simultaneous applications split across two backend processes for one free seat (exactly one succeeded).
