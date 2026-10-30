# 1. Requirements

## Context and assumptions

PHCIS is the system behind primary health care clinics, so I've written this assuming a South African
public health setting. That drives a few concrete choices further down:

- Patients are identified by SA ID number, with passport as the fallback for non-citizens.
- Clinic times are local (`Africa/Johannesburg`). Storage is UTC, but the UI shows clinic time no matter
  which time zone the browser is in.
- Patient details are special personal information under POPIA, so access control and keeping PII out of
  the logs aren't optional.

Things I've deliberately left out of scope for this assessment are listed at the end.

## Stakeholders

| Stakeholder | What they care about |
|---|---|
| **Patient** | Finding an open slot at their clinic, booking without queueing at 5am, getting a reference they can show at reception, cancelling or moving the booking when plans change. |
| **Clinic staff** (reception / nursing) | Seeing today's list, recording who arrived and who didn't, opening up slots for upcoming days. |
| **Facility manager** | No-show rates and slot utilisation. Read-only. Out of scope for this build, but the data supports it. |
| **System administrator** | Managing clinics and staff accounts, keeping the system up, audit trails. |
| **Provincial health department / Information officer** | POPIA compliance, data quality and reporting. |

## Functional requirements

Priority uses MoSCoW. Everything marked **Must** is implemented.

### Patients

| # | Requirement | Priority |
|---|---|---|
| FR-01 | Register with name, SA ID or passport number, date of birth, phone and email. SA IDs are checked (format, date of birth and checksum). | Must |
| FR-02 | Sign in and sign out. | Must |
| FR-03 | Browse clinics. | Must |
| FR-04 | See available slots for a clinic on a given day. Taken or past slots are not offered. | Must |
| FR-05 | Book a slot, with an optional short reason for the visit. | Must |
| FR-06 | Get an on-screen confirmation with a booking reference after booking. | Must |
| FR-07 | View upcoming and past appointments. | Must |
| FR-08 | Cancel an upcoming appointment, with a reason. | Must |
| FR-09 | Reschedule an upcoming appointment to another slot at the same clinic. If the new slot is gone, the original booking stays in place. | Must |
| FR-10 | Receive confirmation by SMS or email. | Should (there is an interface for it; the current implementation only logs) |

### Clinic staff

| # | Requirement | Priority |
|---|---|---|
| FR-11 | View the day list for their own clinic only. | Must |
| FR-12 | Mark an appointment as attended or as a no-show. | Must |
| FR-13 | Generate slots for a date range: daily start and end time, slot length and weekdays. Slots that already exist are skipped. | Must |
| FR-14 | Block a slot (staff shortage, public holiday). | Could |
| FR-15 | Book on behalf of a walk-in patient. | Could |

### Business rules

| # | Rule |
|---|---|
| BR-01 | A slot holds at most one active appointment. This is enforced by the database, not only checked in code. |
| BR-02 | Slots in the past can't be booked. |
| BR-03 | A patient can't hold two active appointments whose times overlap. |
| BR-04 | Only `Booked` appointments can be cancelled or rescheduled, and only before they start. |
| BR-05 | Attendance can only be recorded once the appointment has started. |
| BR-06 | A cancelled appointment frees the slot immediately. |
| BR-07 | Patients only ever see their own appointments. Staff only see their own clinic's. |

## Non-functional requirements

| Area | Requirement | How it's addressed |
|---|---|---|
| **Security** | Authenticated access, role-based authorisation, protection against session theft. | ASP.NET Core Identity with HttpOnly, `SameSite=Strict` cookies. Roles are `Patient` and `Staff`. Resource ownership is checked on every query. Login and registration are rate-limited, and accounts lock after repeated failures. |
| **Privacy (POPIA)** | Minimal PII exposure. | Logs reference records by internal ID only, never by name or ID number. DTOs only carry what the screen needs. |
| **Integrity** | No double bookings, even under concurrent requests. | A partial unique index on active appointments per slot. The API turns a violation into `409 Conflict`. Covered by an integration test that fires concurrent bookings. |
| **Auditability** | Who did what and when. | Appointments are never deleted. Status changes are timestamped and keep the cancellation reason. Reschedules link to the original appointment. |
| **Performance** | Slot lookups stay fast as data grows. | Indexed on `(clinic_id, start_utc)`. Queries use projections and `AsNoTracking`. The target is p95 under 300 ms for slot lookups. |
| **Scalability** | The API can scale out horizontally. | No server-side session state beyond the auth cookie. Data protection keys would move to shared storage when the API scales out (noted, not done). |
| **Availability** | Health monitoring. | `/health` checks the database. Container-ready. |
| **Usability / accessibility** | Works on a cheap Android phone over mobile data. Aims for WCAG 2.1 AA. | Responsive MudBlazor layout, labelled inputs, clear loading and error states. The WASM payload is trimmed by the publish defaults; there is more on this under "next steps" in the decisions log. |
| **Maintainability** | A new developer can find their way around. | Small projects with clear dependency direction, shared contracts and validation, and unit plus integration tests in CI. |

## Out of scope (for now)

- Practitioners and multi-capacity slots (e.g. three nurses seeing patients at 09:00). The data model has a
  clear place for this; see the design doc.
- Real SMS/email delivery (Twilio/SendGrid), calendar sync, SignalR live updates.
- Admin UI for clinics and staff accounts. These are seeded.
- Integration with the existing PHCIS patient master index. In reality, registration would match against
  existing folders rather than create new patients.
