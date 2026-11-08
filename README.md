# Course Inquiry App

Internal web app for managing course registration inquiries.

- **Visitors** submit an inquiry about a course.
- **Staff** view inquiries, filter by status, open an inquiry's details and update its status.
- **Inquiries can be archived** (soft delete). They're hidden from the app but kept in the database.
- **CRM integration**: each new inquiry is sent to an external CRM. The CRM is simulated by `MockCrmClient`, so no real CRM or API keys are needed.

## Stack
| Layer    | Tech                                                    |
|----------|---------------------------------------------------------|
| Frontend | React 19 + TypeScript (Vite): `frontend/`               |
| Backend  | ASP.NET Core Web API (.NET 10) + EF Core 10: `backend/` |
| Database | SQL Server 2022                                         |
| Tests    | xUnit + EF Core InMemory: `backend/CourseInquiry.Api.Tests` |

## Structure
```
backend/CourseInquiry.Api
  Controllers/       InquiriesController
  Models/            Inquiry, InquiryStatus, CrmSyncAttempt
  DTOs/              request/response contracts
  Data/              AppDbContext (EF Core, soft-delete query filter)
  Migrations/        EF Core migrations
  Services/          InquiryService, CrmSyncService (business logic)
  Integrations/Crm/  ICrmClient, MockCrmClient, CrmInquiryPayload, CrmOptions, CrmSyncResult
backend/CourseInquiry.Api.Tests
  Services/          InquiryServiceTests, CrmSyncServiceTests
  Controllers/       InquiriesControllerTests
  DTOs/              CreateInquiryRequestValidationTests
  Fakes/             FakeCrmSyncService
database/
  database.sql       full schema in one script (alternative to EF migrations)
frontend/src
  api/         fetch wrapper + typed API calls
  components/  InquiryForm, InquiryTable, StatusFilter, StatusSelect, StatusBadge, Alert
  pages/       SubmitInquiryPage, StaffDashboardPage, InquiryDetailPage
  hooks/       useInquiries
  types/ utils/
CourseInquiryApp.slnx  solution file (API + tests)
```

---

## Setup & run

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (for SQL Server)
- EF Core CLI: `dotnet tool install --global dotnet-ef` (only needed if you create the database with migrations)

### 1. Start SQL Server
```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<YourStrong!Passw0rd>" \
  -p 1433:1433 --name course-inquiry-sql -d mcr.microsoft.com/mssql/server:2022-latest
```
> On Apple Silicon, turn on *"Use Rosetta for x86/amd64 emulation"* in Docker Desktop settings.

### 2. Configure the connection string
Keep credentials out of `appsettings.json`. Use .NET user secrets instead:
```bash
cd backend/CourseInquiry.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=CourseInquiryDb;User Id=sa;Password=<YourStrong!Passw0rd>;TrustServerCertificate=True"
```

### 3. Create the database
Use **one** of these. Both produce the same schema.

**Option A: EF Core migrations**
```bash
cd backend/CourseInquiry.Api
dotnet ef database update
```

**Option B: SQL script**
```bash
sqlcmd -S localhost,1433 -U sa -P '<YourStrong!Passw0rd>' -C -i database/database.sql
```
The script is safe to run more than once. It also records the migrations in EF's history table, so running `dotnet ef database update` afterwards has nothing to apply.

### 4. Run the API
```bash
cd backend/CourseInquiry.Api
dotnet run --launch-profile http
```
- API: http://localhost:5012/api
- OpenAPI document (Development only): http://localhost:5012/openapi/v1.json

### 5. Run the frontend
```bash
cd frontend
cp .env.example .env      # then set VITE_API_BASE_URL=http://localhost:5012/api
npm install
npm run dev
```
Open http://localhost:5173. The API's CORS policy only allows this origin.

### 6. Run the tests
From the repo root (uses `CourseInquiryApp.slnx`):
```bash
dotnet test
```
Expected result: **50 tests, all passing.**

| Test class | What it covers |
|---|---|
| `InquiryServiceTests` | New inquiries get status `New` and UTC dates, input is normalised, the status filter and ordering work, a status change sets `UpdatedDate` (a repeat of the same status doesn't), archived inquiries are hidden everywhere, and the new inquiry is handed to the CRM |
| `CrmSyncServiceTests` | Success, retry on transient failure, no retry on permanent failure, stopping after `MaxAttempts`, attempt numbering for manual retries, **no personal data in logs**, and a payload that redacts itself |
| `InquiriesControllerTests` | 201 / 200 / 204 / 400 / 404 / 502 responses, case-insensitive status parsing, and invalid input never reaching the service |
| `CreateInquiryRequestValidationTests` | Required fields reject blanks, email format, and the message length limit |

---

## API

| Method | Route                          | Description                                                    |
|--------|--------------------------------|----------------------------------------------------------------|
| POST   | `/api/inquiries`               | Submit an inquiry. Returns 201, or 400 with field errors.     |
| GET    | `/api/inquiries?status=New`    | List active inquiries, newest first. `status` is optional and case-insensitive. |
| GET    | `/api/inquiries/{id}`          | Get one inquiry. Returns 404 if it doesn't exist or is archived. |
| PATCH  | `/api/inquiries/{id}/status`   | Update status. Body: `{ "status": "Contacted" }`.             |
| DELETE | `/api/inquiries/{id}`          | Archive (soft delete). Returns 204, or 404 if it's missing or already archived. |
| GET    | `/api/inquiries/{id}/crm-sync` | History of CRM sync attempts for an inquiry.                  |
| POST   | `/api/inquiries/{id}/crm-sync` | Retry the CRM sync manually. Returns 502 with the reason if it fails. |

Statuses: `New`, `Contacted`, `Pending`, `Registered`, `Closed`.

All errors are returned as [RFC 7807 ProblemDetails](https://www.rfc-editor.org/rfc/rfc7807) JSON, and the UI shows their `detail` or field errors to the user.

## Archiving (soft delete)

`DELETE /api/inquiries/{id}` sets `ArchivedDate` instead of removing the row. A global EF Core query filter (`ArchivedDate == null`) hides archived inquiries from every query: the list, the status filter, get-by-ID, status updates and CRM retries.

- **The row is kept**, along with its original status and its CRM sync history, for auditing.
- **Archiving is a separate column, not an "Archived" status.** The inquiry keeps the status it had (such as "Registered"), and the status filter doesn't need special handling.
- **Code that deliberately needs archived rows** uses `.IgnoreQueryFilters()`.

## CRM integration (simulated)

After an inquiry is saved, `CrmSyncService` maps it to a `CrmInquiryPayload` and sends it through `ICrmClient`:

- **Every attempt is recorded** in the `CrmSyncAttempts` table, with the outcome, CRM reference, a sanitised error message and the duration.
- **Transient failures** such as timeouts and 503s are retried with exponential backoff (`Crm:MaxAttempts`, default 3). **Permanent failures**, such as a validation rejection, are not retried.
- **A CRM failure never fails the submission.** The inquiry is saved first and the failure is recorded, so staff can retry it with `POST /api/inquiries/{id}/crm-sync`.
- **No personal data is logged.** Logs contain only inquiry IDs, attempt numbers, durations and CRM references. `CrmInquiryPayload.ToString()` is overridden to redact its fields, and the API key is never logged. A unit test checks both.

The mock simulates network delay, plus a random transient failure rate (`Crm:SimulatedFailureRate`, default 0.2). You can force an outcome with the email address you submit:

| Email                        | Outcome                                       |
|------------------------------|-----------------------------------------------|
| `jane@example.com`           | Success (random transient failures may be retried first) |
| `jane+crmreject@example.com` | Permanent rejection, no retry                 |
| `jane+crmdown@example.com`   | Outage: every retry fails, then the sync gives up |

Configuration (`appsettings.json`, section `Crm`):

| Setting | Default | Purpose |
|---|---|---|
| `BaseUrl` | `https://example-crm.local/api` | Would be the real CRM endpoint |
| `ApiKey` | *(empty)* | Would be the real key; keep it in user secrets or a key vault, never in source |
| `MaxAttempts` | `3` | Attempts per sync, including the first |
| `RetryBaseDelayMs` | `250` | Backoff base: 250 ms, 500 ms, 1 s… |
| `SimulatedFailureRate` | `0.2` | Mock only: share of random transient failures |

---

## Assumptions

- **Internal tool with no authentication.** The staff dashboard is open to anyone who can reach the app.
- **Free-text course name.** A visitor types the course. There is no course catalogue or dropdown yet.
- **Statuses are stored as their name** (e.g. `"New"`), not as numbers, so the database is readable. Any status can change to any other; there are no workflow rules.
- **Duplicates are allowed.** The same person can submit several inquiries, even for the same course.
- **Emails are trimmed and lowercased.** Optional fields that are blank or whitespace-only are stored as `NULL`.
- **All timestamps are stored in UTC** and converted to local time in the browser.
- **Validation runs on both sides.** The frontend mirrors the server's rules for quick feedback, but the server (DataAnnotations + `[ApiController]`) is the source of truth.
- **Delete means archive.** Nothing is ever deleted outright through the API.
- **The CRM sync runs inside the submit request.** This keeps the simulation simple. In the worst case (CRM down), a submission takes about 2 seconds longer.
- **Local development only.** There is one CORS origin (`http://localhost:5173`) and no deployment configuration.

## What I would improve with more time

**Reliability & integration**
- **Outbox pattern for the CRM.** Write a pending sync row in the same transaction as the inquiry, and have a background worker deliver it. Submissions would stay fast, and retries would survive a restart.
- **A real `HttpClient`-based CRM client** using `IHttpClientFactory`, Polly resilience (retry, circuit breaker, timeout), and an idempotency key so retries can't create duplicate records.
- **Optimistic concurrency** (a `rowversion` column) so two staff members can't silently overwrite each other's status changes.

**Features**
- **Staff authentication and roles**, via ASP.NET Core Identity or Microsoft Entra ID.
- **Archive and CRM in the UI.** The API supports both, but the frontend doesn't yet. I'd add an **Archive** button with a confirmation step and a **CRM sync** panel with a "Retry sync" button on the details page, plus a restore endpoint and an "Archived" view for staff.
- **A course catalogue** (`Courses` table + `GET /api/courses`) so visitors pick from a dropdown.
- **Status change history and staff notes** on each inquiry, and optional rules for which status changes are allowed.
- **Server-side paging, sorting and search** on the inquiry list.
- **React Router**, so the details page has its own URL that can be shared.

**Quality**
- **More tests:**
  - API integration tests with `WebApplicationFactory` + Testcontainers SQL Server, since the InMemory provider doesn't behave exactly like SQL Server
  - frontend tests with Vitest + React Testing Library
  - a Playwright end-to-end test of submit → view → update
- **Read CORS origins from config.** `Cors:AllowedOrigins` already exists in `appsettings.json` but isn't used yet.
- **Rate limiting and a CAPTCHA** on the public submit endpoint.
- **Structured logging** (Serilog / Application Insights) and a health check endpoint.

**DevOps**
- **`docker-compose.yml`** to start SQL Server, the API and the frontend with one command.
- **A CI pipeline** (GitHub Actions) that builds, lints and runs the tests on every PR.
