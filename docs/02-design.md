# 2. System design

## Architecture

A layered monolith: one deployable, with the layers kept honest by project references rather than by
folders and good intentions. For a booking system with four core tables, microservices would mostly
mean distributed transactions around the one thing that has to be atomic (taking a slot), so I didn't
go there. The seams are in place if a part ever needs splitting out. Notifications are the obvious
first candidate.

```mermaid
flowchart LR
    subgraph Browser
        UI["Blazor WASM client<br/>Pages / Components"]
        AC["Typed API clients<br/>ApiResult&lt;T&gt;"]
        UI --> AC
    end

    subgraph Host["ASP.NET Core host (single origin)"]
        SA["Static assets<br/>(serves the WASM app)"]
        EP["Minimal API endpoints<br/>auth policies, validation filter"]
        SV["Application services<br/>IAppointmentService, IClinicScheduleService, ..."]
        DM["Domain<br/>Appointment rules, SlotGenerator"]
        EF["EF Core DbContext"]
        EP --> SV --> DM
        SV --> EF
    end

    DB[("PostgreSQL")]

    AC -- "HTTPS + HttpOnly cookie" --> EP
    Browser -. "first load" .-> SA
    EF --> DB
```

### Projects and dependencies

```mermaid
flowchart TD
    Api[Phcis.Booking.Api] --> Domain[Phcis.Booking.Domain]
    Api --> Shared[Phcis.Booking.Shared]
    Api --> Client[Phcis.Booking.Client]
    Client --> Shared
```

| Project | What's in it | Depends on |
|---|---|---|
| **Domain** | Entities and the booking rules (`Appointment.Book`, `Cancel`, `RescheduleTo`, `RecordAttendance`), slot generation. No EF, no ASP.NET. | nothing |
| **Shared** | DTOs, request models, FluentValidation validators, SA ID number check, route constants. Used by the API and the browser. | FluentValidation |
| **Api** | Endpoints, application services, EF Core mapping and migrations, Identity, seeding. Also hosts the client. | Domain, Shared, Client |
| **Client** | Blazor WebAssembly UI. | Shared |

The Api references the Client only so it can serve the compiled WASM files. No code goes the other way.

I didn't split Application and Infrastructure into their own projects. There's one database and one
host, and the abstraction that matters for testing (`TimeProvider`, `INotificationService`) is an
interface, not an assembly. I'd split them out the day a second host appeared, such as a background
worker sending reminders.

## Data model

```mermaid
erDiagram
    CLINIC ||--o{ TIME_SLOT : offers
    TIME_SLOT ||--o{ APPOINTMENT : "has (at most one active)"
    PATIENT ||--o{ APPOINTMENT : books
    APPOINTMENT |o--o| APPOINTMENT : "rescheduled from"
    IDENTITY_USER |o--o| PATIENT : "online account"

    CLINIC {
        int id PK
        string name
        string address
        string phone_number
        string time_zone_id "IANA, e.g. Africa/Johannesburg"
    }
    TIME_SLOT {
        int id PK
        int clinic_id FK
        timestamptz start_utc "unique with clinic_id"
        timestamptz end_utc "check: end > start"
    }
    PATIENT {
        int id PK
        string user_id FK "nullable: walk-ins have no account"
        string first_name
        string last_name
        string identity_type "SaIdNumber | Passport"
        string identity_number "unique with identity_type"
        date date_of_birth
        string phone_number
        string email
    }
    APPOINTMENT {
        int id PK
        string reference "unique, e.g. PHC-7K3QX9"
        int patient_id FK
        int time_slot_id FK "partial unique index, see below"
        string status "Booked | Cancelled | Rescheduled | Attended | NoShow"
        string reason_for_visit
        timestamptz created_utc
        timestamptz cancelled_utc
        string cancellation_reason
        timestamptz outcome_recorded_utc
        int rescheduled_from_id FK
        xid xmin "optimistic concurrency token"
    }
```

The constraints that carry real weight:

| Constraint | Why |
|---|---|
| `ux_appointments_active_slot`: unique `(time_slot_id) WHERE status IN ('Booked','Attended','NoShow')` | **The double-booking guard.** A slot can collect any number of cancelled or rescheduled appointments, but only one that still holds it. The filter is generated from `AppointmentStatusExtensions.SlotHoldingStatuses`, so the code and the index can't drift apart. |
| `ux_time_slots_clinic_start`: unique `(clinic_id, start_utc)` | Generating slots twice for the same day is harmless. It also serves as the index for "free slots at clinic X on day Y". |
| unique `(identity_type, identity_number)` on patients | One patient record per person. |
| `xmin` row version on appointments | Stops a patient cancelling at the same moment reception marks them as arrived, with the last write silently winning. The loser gets a 409 and a "refresh" message. |
| All FKs `ON DELETE RESTRICT` (patient to user is `SET NULL`) | Appointments are clinical history. Nothing cascades into them. Closing an online account keeps the patient record. |

**Why slots are rows.** The alternative is to store a weekly schedule and work out free times on the
fly. That's less data, but then there's nothing for a booking to point at, and preventing double
bookings means locking or serialisable transactions instead of a unique index. Slot rows also give
staff an obvious way to block a single slot later. The cost is about 25 rows per clinic per day, which
Postgres won't notice.

## Booking flow, including the race

```mermaid
sequenceDiagram
    autonumber
    actor A as Patient A
    actor B as Patient B
    participant API
    participant DB as PostgreSQL

    A->>API: GET /api/clinics/1/slots?date=2026-11-02
    API->>DB: free slots (no slot-holding appointment)
    DB-->>API: 08:00, 08:20, ...
    API-->>A: slots in clinic time (+02:00)
    B->>API: same request
    API-->>B: same slots

    par Both pick 08:20
        A->>API: POST /api/appointments {slotId: 42}
        B->>API: POST /api/appointments {slotId: 42}
    end

    Note over API: Domain rules pass for both: slot is in the future, no overlap for either patient
    API->>DB: INSERT appointment (A)
    API->>DB: INSERT appointment (B)
    DB-->>API: A committed
    DB-->>API: B: unique violation on ux_appointments_active_slot
    API-->>A: 201 Created + reference PHC-7K3QX9
    API-->>B: 409 Conflict (ProblemDetails)
    Note over B: Client shows "just been taken" and reloads the slot list
```

There's deliberately no "is this slot free?" check in the booking code. It would make the happy path
look safer and do nothing for the race. `BookingTests.Concurrent_bookings_for_the_same_slot_only_one_wins`
fires ten bookings at once against real Postgres to prove this.

**Rescheduling** marks the old appointment `Rescheduled` and inserts the new one in a single
`SaveChanges`, which is one transaction. If the new slot is gone, the insert fails, the update rolls
back with it, and the patient keeps their original time.

## Appointment lifecycle

```mermaid
stateDiagram-v2
    [*] --> Booked: Book (slot in the future)
    Booked --> Cancelled: Cancel (before start, reason required)
    Booked --> Rescheduled: RescheduleTo (before start, same clinic)
    Booked --> Attended: RecordAttendance (after start, staff)
    Booked --> NoShow: RecordAttendance (after start, staff)
    Cancelled --> [*]
    Rescheduled --> [*]: replacement appointment carries on as Booked
    Attended --> [*]
    NoShow --> [*]
```

`Cancelled` and `Rescheduled` free the slot. `Attended` and `NoShow` keep holding it, since the time has passed anyway.

## API

| Method | Route | Who | Notes |
|---|---|---|---|
| POST | `/api/account/register` | anyone | Creates account + patient record, signs in. Rate-limited. |
| POST | `/api/account/login` | anyone | Rate-limited, lockout after 5 failures. |
| POST | `/api/account/logout` | signed in | |
| GET | `/api/account/me` | anyone | 200 with user info, or 204 when signed out. |
| GET | `/api/clinics` | anyone | |
| GET | `/api/clinics/{id}/slots?date=` | anyone | Free, future slots for one clinic-local day. |
| GET | `/api/appointments` | patient | Own appointments only. |
| GET | `/api/appointments/{reference}` | patient | Someone else's reference returns 404, not 403. |
| POST | `/api/appointments` | patient | 201, 409 if taken, 422 if a rule fails. |
| POST | `/api/appointments/{reference}/cancel` | patient | |
| POST | `/api/appointments/{reference}/reschedule` | patient | |
| GET | `/api/staff/day-list?date=` | staff | Clinic comes from the staff member's claim, never the request. ID numbers masked. |
| POST | `/api/staff/appointments/{id}/attendance` | staff | |
| POST | `/api/staff/slots` | staff | Idempotent per start time. |
| GET | `/health` | anyone | Includes a database check. |

Errors are always RFC 9457 `ProblemDetails`. Validation errors come back keyed by property name, and
the client shows them next to the matching field. Full OpenAPI at `/openapi/v1.json`, browsable at
`/scalar/v1` in Development.

## Time

- Stored as `timestamptz` in UTC.
- Each clinic has an IANA time zone. Slot generation takes clinic-local times ("08:00 to 16:00") and
  converts them, skipping local times that don't exist (DST gaps; SA has none, but the zone is data).
- DTOs carry `DateTimeOffset` with the **clinic's** offset, so a browser in London still shows 09:00
  for a 09:00 appointment in Cape Town.
- Everything that asks "what time is it?" goes through `TimeProvider`, so tests can pin the clock.

## Client structure

| Piece | Role |
|---|---|
| `CookieAuthenticationStateProvider` | Asks `/api/account/me` who the (HttpOnly) cookie belongs to and caches the answer. Sign-in, sign-out and any 401 update it, and `AuthorizeRouteView` redirects to login. |
| `ApiClientBase` and `IBookingApi` / `IStaffApi` / `IAccountApi` | Typed wrappers that return `ApiResult<T>` instead of throwing. Pages handle success and failure without try/catch, and the interfaces make pages easy to test with bUnit. |
| `FluentValidator<TModel>` | Connects the shared validators to `EditForm`, and can show server-side field errors in the same place. |
| `SlotPicker`, `AppointmentCard`, `StatusChip`, `CancelAppointmentDialog` | Reusable components. `SlotPicker` supports `@bind-SelectedSlotId`. |
