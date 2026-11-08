namespace CourseInquiry.Api.Services;

using System.Diagnostics;
using CourseInquiry.Api.Data;
using CourseInquiry.Api.DTOs;
using CourseInquiry.Api.Integrations.Crm;
using CourseInquiry.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public class CrmSyncService : ICrmSyncService
{
    private readonly ICrmClient _client;
    private readonly AppDbContext _db;
    private readonly CrmOptions _options;
    private readonly ILogger<CrmSyncService> _logger;

    public CrmSyncService(ICrmClient client, AppDbContext db, IOptions<CrmOptions> options, ILogger<CrmSyncService> logger)
    {
        _client = client;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CrmSyncResult> SyncInquiryAsync(Inquiry inquiry, CancellationToken ct = default)
    {
        var payload = CrmInquiryPayload.FromInquiry(inquiry);
        var maxAttempts = Math.Max(1, _options.MaxAttempts);

        // Continue numbering after any earlier attempts (e.g. a manual retry)
        var previousAttempts = await _db.CrmSyncAttempts.CountAsync(a => a.InquiryId == inquiry.Id, CancellationToken.None);
        var result = CrmSyncResult.Failed("Not attempted.", isTransient: false);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                result = await _client.SendInquiryAsync(payload, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                result = CrmSyncResult.Failed("Sync cancelled before completion.", isTransient: false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TimeoutException or TaskCanceledException)
            {
                // Transport failure: store a generic message, not the raw exception text
                result = CrmSyncResult.Failed($"CRM unavailable ({ex.GetType().Name}).", isTransient: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error syncing inquiry {InquiryId} to CRM", inquiry.Id);
                result = CrmSyncResult.Failed("Unexpected error during CRM sync.", isTransient: false);
            }
            sw.Stop();

            await RecordAttemptAsync(inquiry.Id, previousAttempts + attempt, result, sw.Elapsed);

            if (result.Success)
            {
                _logger.LogInformation(
                    "CRM sync succeeded for inquiry {InquiryId} on attempt {Attempt} (ref {CrmReference}, {DurationMs} ms)",
                    inquiry.Id, attempt, result.CrmReference, sw.ElapsedMilliseconds);
                return result;
            }

            if (!result.IsTransient || attempt == maxAttempts) break;

            var delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs * Math.Pow(2, attempt - 1));
            _logger.LogWarning(
                "CRM sync attempt {Attempt}/{MaxAttempts} failed for inquiry {InquiryId}: {Error}. Retrying in {DelayMs} ms",
                attempt, maxAttempts, inquiry.Id, result.ErrorMessage, delay.TotalMilliseconds);
            await Task.Delay(delay, CancellationToken.None);
        }

        _logger.LogWarning(
            "CRM sync FAILED for inquiry {InquiryId}: {Error} (transient: {IsTransient})",
            inquiry.Id, result.ErrorMessage, result.IsTransient);
        return result;
    }

    public async Task<CrmSyncResult?> RetrySyncAsync(int inquiryId, CancellationToken ct = default)
    {
        var inquiry = await _db.Inquiries.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inquiryId, ct);
        return inquiry is null ? null : await SyncInquiryAsync(inquiry, ct);
    }

    public async Task<IReadOnlyList<CrmSyncAttemptResponse>?> GetAttemptsAsync(int inquiryId, CancellationToken ct = default)
    {
        if (!await _db.Inquiries.AnyAsync(i => i.Id == inquiryId, ct)) return null;

        var attempts = await _db.CrmSyncAttempts
            .AsNoTracking()
            .Where(a => a.InquiryId == inquiryId)
            .OrderBy(a => a.AttemptNumber)
            .ToListAsync(ct);

        return attempts.Select(a => new CrmSyncAttemptResponse
        {
            AttemptNumber = a.AttemptNumber,
            Succeeded = a.Succeeded,
            IsTransientFailure = a.IsTransientFailure,
            CrmReference = a.CrmReference,
            ErrorMessage = a.ErrorMessage,
            DurationMs = a.DurationMs,
            AttemptedAt = DateTime.SpecifyKind(a.AttemptedAt, DateTimeKind.Utc)
        }).ToList();
    }

    private async Task RecordAttemptAsync(int inquiryId, int attemptNumber, CrmSyncResult result, TimeSpan elapsed)
    {
        var attempt = new CrmSyncAttempt
        {
            InquiryId = inquiryId,
            AttemptNumber = attemptNumber,
            Succeeded = result.Success,
            IsTransientFailure = !result.Success && result.IsTransient,
            CrmReference = result.CrmReference,
            ErrorMessage = result.ErrorMessage,
            DurationMs = (int)elapsed.TotalMilliseconds,
            AttemptedAt = DateTime.UtcNow
        };

        try
        {
            _db.CrmSyncAttempts.Add(attempt);
            // Record even if the HTTP request was cancelled
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // An audit failure must not break the inquiry flow
            _db.Entry(attempt).State = EntityState.Detached;
            _logger.LogError(ex, "Failed to record CRM sync attempt {Attempt} for inquiry {InquiryId}",
                attemptNumber, inquiryId);
        }
    }
}
