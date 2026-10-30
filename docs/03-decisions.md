# 3. Decisions and trade-offs

The brief lists a lot of technology. I used what earned its place and left the rest out on purpose.
Each entry says what I chose, why, and what it costs.

---

### Cookies instead of JWT

**Chose:** ASP.NET Core Identity with an HttpOnly, `Secure`, `SameSite=Strict` cookie.

**Why:** The WASM client is served from the same origin as the API. A JWT would have to live somewhere
JavaScript can read it (localStorage or memory), and an XSS bug would then leak a bearer token that
works from anywhere. An HttpOnly cookie can't be read by script at all. For a same-origin SPA this is
also what Microsoft's own guidance recommends. `SameSite=Strict` deals with CSRF for this setup.

**Cost:** Doesn't suit third-party API consumers or a mobile app. If one turns up, add a JWT bearer
scheme next to the cookie for those callers. Nothing about the endpoints would change. Worth knowing:
the "bearer tokens" from .NET's `MapIdentityApi` aren't JWTs either, so that wouldn't have ticked the
box as written.

---

### Double bookings are stopped by the database, not by code

**Chose:** A partial unique index: one slot-holding appointment per slot. A violation becomes `409 Conflict`.

**Why:** "Check if free, then insert" is a race. Two requests both see the slot as free. Locking
(`SELECT ... FOR UPDATE`) or serialisable isolation would work, but they cost more and are easier to
get wrong. The index is one line of configuration that can't be bypassed, and it's covered by an
integration test that fires ten concurrent bookings at the same slot.

**Cost:** Tied to databases with filtered/partial indexes (Postgres and SQL Server both have them).
Capacity greater than one per slot would need a different shape. See "what's next".

---

### PostgreSQL

**Chose:** Postgres, from the two the brief allows.

**Why:** Partial indexes, `xmin` as a free concurrency token, `timestamptz`, and a small Docker image
for local runs and CI. SQL Server would work equally well (filtered index, `rowversion`). Swapping
means changing the provider package and regenerating migrations.

---

### Integration tests against real Postgres, not EF InMemory

**Why:** The InMemory provider doesn't enforce unique indexes, transactions or concurrency tokens,
which are exactly the things most worth testing here. Each test run creates its own database and
drops it afterwards. CI uses a Postgres service container.

---

### No repository pattern or unit of work on top of EF Core

**Why:** `DbContext` already is a unit of work and `DbSet` already is a repository. Wrapping them adds
a layer that has to be mocked, and mocked EF tests prove very little. Services use the context
directly, and I test them against a real database instead.

---

### No AutoMapper, no MediatR

**Why:** Both moved to commercial licences in 2025, which is a real procurement question for a
government system. Beyond that, mapping four entities by hand is about 40 lines that the compiler
checks. A renamed property breaks the build instead of turning into a null at runtime. With a dozen
endpoints, MediatR would be indirection without a payoff. The same goes for FluentAssertions (also
commercial now): tests use plain xUnit asserts.

---

### Minimal APIs with route groups

**Why:** Less ceremony than controllers. Route groups let a whole area share one authorisation policy
(`Patient`, `Staff`). `TypedResults` keeps the OpenAPI output accurate. Validation runs as an endpoint
filter using the same FluentValidation validators the client uses.

---

### Shared validation between client and server

**Why:** The rules are written once, in `Phcis.Booking.Shared`. The client runs them for instant
feedback through a small `FluentValidator<T>` component on `EditForm`. The server runs them again
because the client can't be trusted. If the server rejects something anyway (e.g. email already
registered), its field errors show up in the same place on the form.

---

### Rule violations are exceptions on the server, results on the client

**Server:** Domain methods throw `BookingRuleException`/`SlotUnavailableException`. One
`IExceptionHandler` turns them into `ProblemDetails` (422/409/404). Endpoints stay readable, and an
unexpected exception still becomes a logged 500.

**Client:** API calls return `ApiResult<T>`. A UI always has to handle failure, so it's part of the
return type rather than a try/catch the page might forget.

---

### Slots are generated rows, not calculated from a schedule

**Why:** It gives bookings a real row to point at, which is what makes the unique index possible.
Staff open up slots for a date range. Running it twice is safe because existing start times are
skipped. The seeder keeps two weeks of slots topped up for the demo.

**Cost:** Changing a clinic's hours means generating new slots rather than editing a template.

---

### Time zones

UTC in the database. Each clinic has an IANA zone. DTOs carry `DateTimeOffset` with the clinic's
offset, so the UI shows clinic time even if the browser is somewhere else. `TimeProvider` everywhere,
so tests control the clock.

---

### Hosting the WASM client from the API

**Why:** One origin (cookies just work, no CORS), one container, one thing to deploy.

**Gotcha I hit:** In .NET 10 the old `UseBlazorFrameworkFiles()` middleware conflicts with
`MapStaticAssets()`: framework files returned 500. `MapStaticAssets` handles it alone. Separately, the
`#[.{fingerprint}]` placeholder in `index.html` isn't rewritten when the client is published through
the host project, which gave a blank page in Release only. `index.html` now uses the plain
`blazor.webassembly.js` route. I caught this by running the full browser flow against a Release
publish, not just a dev build.

---

### Privacy (POPIA)

- Logs reference records by ID and booking reference only. No names, ID numbers or phone numbers.
- Staff see the last four characters of a patient's ID number, which is enough to match a document at reception.
- A booking reference belonging to someone else returns 404, not 403. The API doesn't confirm it exists.
- Patients without an online account (walk-ins) are supported: `user_id` is nullable, and deleting
  an account keeps the clinical record.

---

## Known gaps

Things I'd flag in a code review of my own work:

1. **A patient's overlapping bookings are only checked in code.** Two simultaneous bookings by the
   same person at different clinics could both succeed. The fix is a Postgres exclusion constraint on
   `(patient_id, tstzrange(start, end))` with `btree_gist`. That needs the slot times copied onto the
   appointment row, so I left it for now.
2. **Notifications are sent inline after saving** (and only logged). Production needs an outbox table
   and a background sender, so a slow SMS gateway can't affect bookings and nothing is lost on a crash.
3. **Data protection keys** are stored per container. With more than one instance they need shared
   storage (database or blob), otherwise users get signed out when load-balanced to another node.
4. **Walk-in patients can't claim their record online.** Registration with an ID number that already
   exists is refused with "ask at your clinic". Linking needs proper identity verification (OTP to the
   phone number on file, for example).
5. **No security headers yet** (CSP with `wasm-unsafe-eval`, `X-Content-Type-Options`, etc.).

## What's next, roughly in order

1. Outbox and real SMS reminders the day before (biggest impact on no-show rates).
2. Practitioners and slot capacity: a slot belongs to a practitioner, or carries a capacity with a
   count check. The unique-index approach would move to `(slot_id, seat_number)`.
3. Staff blocking individual slots, and booking on behalf of walk-in patients.
4. SignalR to push "slot taken" to anyone viewing the same day, so fewer people ever see a 409.
5. Playwright end-to-end tests in CI. I ran the main flows through Playwright by hand while building
   this, but they're not in the pipeline.
6. Accessibility audit (axe) and checking it on a low-end Android over 3G. The WASM download is the
   main cost there. Lazy-loading the staff pages' assembly would help.
