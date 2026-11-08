# Part 5 – Written Answers

## Troubleshooting: inquiries missing from the admin list

**1. Narrow down the report first**
- Ask the staff member for specific examples: the visitor's name or email, the course, the rough submission time, and what the visitor saw after clicking submit (success message, error, or nothing).
- Check whether it affects every submission or only some. "Only some" points to a particular condition, such as certain input, a time window, or one browser, rather than a complete outage.
- Check the dashboard filter. If a status filter is active, an inquiry in a different status won't show. I'd rule this out before looking at anything technical.

**2. Logs and systems to check**
- **Browser:** DevTools Network tab. Look at the status of the `POST /api/inquiries` request (201, 400, 500, a CORS failure or no request at all) and the Console for JavaScript errors.
- **API logs:** request logs for `POST /api/inquiries` around the reported time, any unhandled exceptions (returned as ProblemDetails 500s), and validation 400s.
- **Database:** whether the row exists at all (queries below).
- **CRM sync log:** the `CrmSyncAttempts` table and the CRM sync warnings in the logs, to rule out the integration.
- **Infrastructure:** the reverse proxy or load balancer logs (did the request reach the API?), recent deployments, and configuration changes. A changed connection string, for example, could mean the API is writing to a different database from the one the admin list reads.

**3. Frontend, backend, database or integration?**

| Evidence | Likely layer |
|---|---|
| No request in the Network tab, or a CORS / JS error | **Frontend**: form bug, or a wrong `VITE_API_BASE_URL` |
| The request returns 400 | **Validation**: the user's input is rejected. Check that the UI shows the error clearly, because the user may think it went through |
| The request returns 500, or there's no response | **Backend**: check the exception in the API logs |
| 201 is returned but there's no row in the database | **Database / config**: wrong database or environment, or a transaction issue |
| The row exists but isn't in the list | **Backend read path or frontend**: the status filter, the query's `WHERE` / `ORDER BY`, or the list not refreshing |
| The row exists and the list is fine, but it's missing from the CRM | **Integration**: check `CrmSyncAttempts` |

I'd try to reproduce it end to end with test data in a non-production environment, using the details from the failing examples.

**4. SQL queries**
```sql
-- Does the inquiry exist?
SELECT * FROM dbo.Inquiries
WHERE Email = 'jane@example.com'          -- stored lowercased
ORDER BY CreatedDate DESC;

-- What was saved around the reported time? (CreatedDate is UTC)
SELECT Id, Email, CourseName, Status, CreatedDate
FROM dbo.Inquiries
WHERE CreatedDate BETWEEN '2026-11-08 09:00' AND '2026-11-08 11:00'
ORDER BY CreatedDate;

-- Are submissions arriving at all? Count per hour, to spot gaps
SELECT CAST(CreatedDate AS date) AS Day, DATEPART(hour, CreatedDate) AS Hour, COUNT(*) AS Total
FROM dbo.Inquiries
WHERE CreatedDate >= DATEADD(day, -7, SYSUTCDATETIME())
GROUP BY CAST(CreatedDate AS date), DATEPART(hour, CreatedDate)
ORDER BY Day, Hour;

-- Unexpected status values that the UI filter wouldn't match
SELECT Status, COUNT(*) FROM dbo.Inquiries GROUP BY Status;

-- Failed CRM syncs
SELECT a.InquiryId, a.AttemptNumber, a.ErrorMessage, a.AttemptedAt
FROM dbo.CrmSyncAttempts a
WHERE a.Succeeded = 0
ORDER BY a.AttemptedAt DESC;

-- Am I connected to the database I think I am?
SELECT DB_NAME() AS CurrentDatabase, @@SERVERNAME AS ServerName;
```
I'd also keep in mind that times are stored in UTC. A staff member searching by local time could be looking at the wrong time window.

**5. Communicating with non-technical stakeholders**
- Acknowledge the report quickly, and state the impact plainly: "Some inquiries may not be showing in the admin list. We're investigating."
- Send updates at a regular interval (for example, every couple of hours) even when nothing has changed, focused on impact rather than technical detail. For example: "We've confirmed the inquiries are being received and saved; the problem is with how the list displays them."
- Give a workaround where possible, such as "Clear the status filter", or "We can send you the affected inquiries directly."
- Once it's resolved, send a short summary: what happened, how many inquiries were affected, whether any data was lost, and what has changed so it doesn't happen again.

**6. Preventing it from happening again**
- Add an automated test that reproduces the bug before fixing it, so it can't come back unnoticed.
- Add monitoring and alerts: an unusual rise in 4xx/5xx errors on `POST /api/inquiries`, an hour with no submissions during business hours, and failed CRM syncs.
- Use correlation IDs so a single submission can be traced from the browser through the API to the database.
- Add a health check endpoint, and a post-deploy smoke test that submits an inquiry and reads it back.
- Make sure every failure produces a clear message in the UI. A silent failure is how a visitor ends up believing a submission went through when it didn't.
- Do a blameless post-incident review and record what was learned.

---

## Security

**Input validation**
- The server is the source of truth. DataAnnotations on the request DTOs (required fields, maximum lengths, email format) are enforced automatically by `[ApiController]`. The frontend runs the same checks, but only to give quick feedback.
- The API accepts DTOs, never the database entities directly. Clients can't set fields they shouldn't, such as `Id`, `Status` or `CreatedDate` (this is called over-posting).
- Status values are checked against the enum, including `Enum.IsDefined`, so `?status=99` or `{ "status": "Admin" }` gets a 400.
- Field lengths match the database column limits, so oversized input is rejected before it reaches SQL.

**SQL injection prevention**
- All data access goes through EF Core LINQ, which always sends values as SQL parameters.
- The app builds no SQL by joining strings. If raw SQL were ever needed, I'd use `FromSql` with interpolated parameters, never `FromSqlRaw` with concatenated values.
- The database account would have least-privilege access: only the permissions it needs on the app's tables, not `sa`.

**Authentication and authorization**
- *Not implemented yet. This is the biggest gap.* The public submit endpoint stays anonymous. Everything else (list, details, status update, CRM retry) needs staff authentication, for example Microsoft Entra ID or ASP.NET Core Identity with cookies or JWT, plus `[Authorize]` with a staff role or policy.
- I'd add rate limiting (`AddRateLimiter`) and a CAPTCHA to the public form to stop spam and abuse.
- CORS is limited to the frontend's origin. I'd move that origin into configuration for each environment.

**Error handling**
- `AddProblemDetails()` and `UseExceptionHandler()` return a consistent RFC 7807 error response. Stack traces and exception details are never shown outside Development.
- Expected errors such as 400 and 404 return clear, safe messages. Unexpected errors return a generic message, and the details go only to the server logs.

**Logging**
- Structured logs contain only identifiers (inquiry ID, attempt number, CRM reference), never names, emails, phone numbers or message text.
- `CrmInquiryPayload.ToString()` is overridden to redact its fields, so logging the object by accident leaks nothing.
- The CRM sync log stores error messages written by our own code, not raw exception text that could contain payload data.

**Sensitive data handling**
- Secrets such as connection strings and the CRM API key stay out of source control. That means user secrets locally and Key Vault or environment variables when deployed.
- HTTPS everywhere, with HSTS in production. SQL Server connections are encrypted with proper certificate validation; `TrustServerCertificate=True` is for local development only.
- Personal data is kept to a minimum. I'd define how long inquiries are kept and delete old ones on a schedule, in line with privacy law such as GDPR.
- Dependency vulnerability scanning (`dotnet list package --vulnerable`, `npm audit`, Dependabot). This build already flags a vulnerable `System.Security.Cryptography.Pkcs` package that I'd update.

---

## Accessibility

1. **Labelled form fields with linked error messages.** Every input has a `<label htmlFor>`. A field with an error gets `aria-invalid="true"` and `aria-describedby` pointing at its error text, so screen readers read the error with the field. Required fields are marked both visually and in the text.
2. **Announced status messages.** Error alerts use `role="alert"` and success messages use `role="status"`, so screen-reader users hear the outcome of a submit or a status change without having to search for it. Error messages explain what to fix ("Please enter a valid email address (e.g. name@example.com)") instead of only saying something is wrong.
3. **Keyboard access, and not relying on colour alone.** Every action is a real `<button>` or form control with a visible focus outline. Clickable table rows also have a "View" button, so keyboard users aren't stuck with a mouse-only row click. Status badges show the status as text, not just a colour, and the filter buttons expose `aria-pressed`. With more time I'd check colour contrast against WCAG AA and run axe on each page.

---

## Code quality: how I organised the solution

- **Layered backend with one job per layer:**
  - **Controllers** handle HTTP only: routing, status codes, and turning results into ProblemDetails.
  - **Services** hold the business rules: normalising input, the default status, recording an update only when the status actually changes.
  - **`AppDbContext`** handles data access.
  - **`Integrations/Crm`** isolates the external system.
- **DTOs are separate from the database entities**, so the API contract can change without changing the database, and clients can't over-post fields.
- **Code depends on interfaces** (`IInquiryService`, `ICrmClient`, `ICrmSyncService`) registered through dependency injection, which makes it easy to substitute fakes in tests. The real CRM client can replace `MockCrmClient` with a one-line change in `Program.cs`.
- **A CRM failure is treated as recoverable, not fatal.** The inquiry is always saved first, and every sync attempt is recorded so staff can retry.
- **The frontend mirrors this separation:**
  - `api/` holds a typed fetch wrapper and turns errors into one consistent shape.
  - `hooks/` holds data loading.
  - `components/` holds reusable UI.
  - `pages/` composes those into screens.
  - Shared types live in `types/`.
- **Tests cover behaviour that matters:** the service's business rules (in-memory database) and the validation rules (DataAnnotations), named so each one reads as a specification.
- **What I'd tighten with more time:**
  - API integration tests with `WebApplicationFactory`
  - frontend tests
  - authentication
  - moving the CRM sync to a background worker with an outbox table
