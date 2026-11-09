# Assumptions

Where the brief was silent, these are the choices made, and why.

## Domain rules
- **Duplicate class names are allowed.** IDs identify classes, and automated tests may create "Class 6 - Morning Shift" repeatedly.
- **No login and no roles.** Class creation is open, as there is no authentication; the student is the `X-Student-Id` header.
- **A confirmed admission cannot be cancelled** (409 `CANNOT_CANCEL_CONFIRMED`): the fee is paid and refunds are out of scope. Cancelling is for unpaid applications.
- **Repeating confirm returns 200 and repeating cancel returns 204**, so client retries are safe; `confirmedSeats` rises only once.
- **Another student's application returns 403.** `GET /holds/{id}` needs no header (per the API list) and never returns the student's number; IDs are random (UUIDv7), so they cannot be guessed.
- **After EXPIRED or CANCELLED the student may apply again** to the same class. A student with a confirmed admission may not.
- **A student may hold seats in different classes at the same time**; the limit is one active application or confirmed admission *per class*.
- **The hold length is fixed when the hold is created**; changing `HOLD_DURATION_SECONDS` later does not change existing holds.
- **Limits:** class name 1 to 100 characters, seats 1 to 100000, student ID exactly 17 ASCII digits (the UI converts Bangla digits).
- IDs are random, prefixed values (`c_...`, `h_...`).

## Seat safety under concurrency (also with two backend processes)
- Applying runs in one transaction that **locks the class row** (`SELECT ... FOR UPDATE`), releases that class's expired holds, counts held + confirmed seats and inserts only if a seat is free. Every request that could add to the count takes the same lock, so the check and the insert are atomic per class. The lock lives in PostgreSQL, so it works for any number of backend processes; nothing relies on in-memory state.
- **Confirm and cancel are single conditional `UPDATE`s** (owner, status HELD, not expired). Simultaneous or repeated confirms change the row at most once.
- Seat numbers are **counted from the holds**, never stored, so they cannot drift.
- A **partial unique index** (class, student) for HELD/CONFIRMED rows backs up the one-application-per-student rule.
- Rejected alternatives: a counter column (expiry would need a job to decrement it, so seats would return late and drift), in-process locks (break with a second process), serializable transactions with retry (more complex for no gain).
- The connection pool defaults to 30 per process (below PostgreSQL's default of 100 connections), so a burst queues instead of failing.

## Expiry
- A hold counts as expired **the moment its time passes**: every query judges it against the database clock, so the seat is free without any API call or background job. A housekeeping job only rewrites the stored status.
- The **database clock is the single clock**. Responses carry `serverTime`, and the UI countdown uses it, so it is right even if the user's computer clock is wrong.

## User interface
- The UI is in Bangla with the status codes (`HELD`, `EXPIRED`, ...) kept visible next to a Bangla meaning, never by colour alone.
- The student number is kept in the browser's local storage so a reload or reopened browser still shows the running application.
- Seat counts refresh every 3 seconds; a running application every 5 seconds. Polling pauses in background tabs.
- "ভর্তি ফি দিন" simply confirms: there is no payment gateway.
- Cancelling asks for a second confirmation (inline, no browser dialog).

## Technology
- **.NET 10 (LTS)** rather than 8, because .NET 8 support ends in November 2026.
- The frontend is configured at container start through `/config.js`, so the backend URL changes without a rebuild; the browser calls the backend directly, which is why CORS is enabled.
