namespace CourseInquiry.Api.Tests.Services;

using CourseInquiry.Api.Data;
using CourseInquiry.Api.Integrations.Crm;
using CourseInquiry.Api.Models;
using CourseInquiry.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class CrmSyncServiceTests
{
    // ---------- test doubles ----------

    /// Plays back a scripted sequence: each step either returns a result or throws.
    private sealed class ScriptedCrmClient : ICrmClient
    {
        private readonly Queue<Func<CrmSyncResult>> _steps;
        public int Calls { get; private set; }

        public ScriptedCrmClient(params Func<CrmSyncResult>[] steps) => _steps = new(steps);

        public Task<CrmSyncResult> SendInquiryAsync(CrmInquiryPayload payload, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(_steps.Dequeue()());
        }
    }

    /// Captures every formatted log message so tests can inspect them.
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private static readonly Func<CrmSyncResult> Ok = () => CrmSyncResult.Succeeded("CRM-OK000001");
    private static readonly Func<CrmSyncResult> Outage = () => throw new HttpRequestException("503");
    private static readonly Func<CrmSyncResult> Rejected = () => CrmSyncResult.Failed("CRM rejected the record (validation error).", isTransient: false);

    // ---------- helpers ----------

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Inquiry SeedInquiry(AppDbContext db)
    {
        var inquiry = new Inquiry
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            Phone = "0412345678",
            CourseName = "Intro to C#",
            Message = "Please call me back",
            Status = "New",
            CreatedDate = DateTime.UtcNow
        };
        db.Inquiries.Add(inquiry);
        db.SaveChanges();
        return inquiry;
    }

    private static CrmSyncService CreateService(AppDbContext db, ICrmClient client, CapturingLogger<CrmSyncService>? logger = null) =>
        new(client, db,
            Options.Create(new CrmOptions { MaxAttempts = 3, RetryBaseDelayMs = 0 }),   // no waiting in tests
            logger ?? new CapturingLogger<CrmSyncService>());

    // ---------- tests ----------

    [Fact]
    public async Task Success_FirstAttempt_RecordsOneSuccessfulAttempt()
    {
        using var db = CreateDb();
        var inquiry = SeedInquiry(db);
        var client = new ScriptedCrmClient(Ok);

        var result = await CreateService(db, client).SyncInquiryAsync(inquiry);

        Assert.True(result.Success);
        Assert.Equal("CRM-OK000001", result.CrmReference);
        var attempt = Assert.Single(await db.CrmSyncAttempts.ToListAsync());
        Assert.True(attempt.Succeeded);
        Assert.Equal("CRM-OK000001", attempt.CrmReference);
        Assert.Equal(inquiry.Id, attempt.InquiryId);
    }

    [Fact]
    public async Task TransientFailure_IsRetried_ThenSucceeds()
    {
        using var db = CreateDb();
        var inquiry = SeedInquiry(db);
        var client = new ScriptedCrmClient(Outage, Ok);

        var result = await CreateService(db, client).SyncInquiryAsync(inquiry);

        Assert.True(result.Success);
        Assert.Equal(2, client.Calls);
        var attempts = await db.CrmSyncAttempts.OrderBy(a => a.AttemptNumber).ToListAsync();
        Assert.Equal(new[] { 1, 2 }, attempts.Select(a => a.AttemptNumber));
        Assert.False(attempts[0].Succeeded);
        Assert.True(attempts[0].IsTransientFailure);
        Assert.True(attempts[1].Succeeded);
    }

    [Fact]
    public async Task PermanentFailure_IsNotRetried()
    {
        using var db = CreateDb();
        var inquiry = SeedInquiry(db);
        var client = new ScriptedCrmClient(Rejected, Ok);

        var result = await CreateService(db, client).SyncInquiryAsync(inquiry);

        Assert.False(result.Success);
        Assert.False(result.IsTransient);
        Assert.Equal(1, client.Calls);   // gave up immediately
        var attempt = Assert.Single(await db.CrmSyncAttempts.ToListAsync());
        Assert.Equal("CRM rejected the record (validation error).", attempt.ErrorMessage);
    }

    [Fact]
    public async Task Outage_StopsAfterMaxAttempts_AndDoesNotThrow()
    {
        using var db = CreateDb();
        var inquiry = SeedInquiry(db);
        var client = new ScriptedCrmClient(Outage, Outage, Outage, Ok);

        var result = await CreateService(db, client).SyncInquiryAsync(inquiry);

        Assert.False(result.Success);
        Assert.True(result.IsTransient);
        Assert.Equal(3, client.Calls);   // MaxAttempts = 3, the 4th step is never reached
        Assert.Equal(3, await db.CrmSyncAttempts.CountAsync(a => !a.Succeeded));
    }

    [Fact]
    public async Task ManualRetry_ContinuesAttemptNumbering()
    {
        using var db = CreateDb();
        var inquiry = SeedInquiry(db);
        var service = CreateService(db, new ScriptedCrmClient(Rejected, Ok));
        await service.SyncInquiryAsync(inquiry);          // attempt 1: rejected

        var result = await service.RetrySyncAsync(inquiry.Id);   // attempt 2: ok

        Assert.True(result!.Success);
        var history = await service.GetAttemptsAsync(inquiry.Id);
        Assert.Equal(new[] { 1, 2 }, history!.Select(a => a.AttemptNumber));
    }

    [Fact]
    public async Task RetryAndHistory_UnknownInquiry_ReturnNull()
    {
        using var db = CreateDb();
        var service = CreateService(db, new ScriptedCrmClient());

        Assert.Null(await service.RetrySyncAsync(999));
        Assert.Null(await service.GetAttemptsAsync(999));
    }

    [Fact]
    public async Task Logs_NeverContainPersonalData()
    {
        using var db = CreateDb();
        var inquiry = SeedInquiry(db);
        var logger = new CapturingLogger<CrmSyncService>();
        var service = CreateService(db, new ScriptedCrmClient(Outage, Rejected), logger);

        await service.SyncInquiryAsync(inquiry);

        Assert.NotEmpty(logger.Messages);
        var allLogs = string.Join("\n", logger.Messages);
        Assert.Contains(inquiry.Id.ToString(), allLogs);   // identifiers are fine
        foreach (var pii in new[] { inquiry.FirstName, inquiry.LastName, inquiry.Email, inquiry.Phone!, inquiry.Message! })
            Assert.DoesNotContain(pii, allLogs);
    }

    [Fact]
    public void Payload_ToString_RedactsPersonalData()
    {
        using var db = CreateDb();
        var payload = CrmInquiryPayload.FromInquiry(SeedInquiry(db));

        var text = payload.ToString();

        Assert.Contains("[REDACTED]", text);
        Assert.DoesNotContain("jane.doe@example.com", text);
        Assert.DoesNotContain("Jane", text);
    }
}
