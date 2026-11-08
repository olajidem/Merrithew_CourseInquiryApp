namespace CourseInquiry.Api.Tests.Fakes;

using CourseInquiry.Api.DTOs;
using CourseInquiry.Api.Integrations.Crm;
using CourseInquiry.Api.Models;
using CourseInquiry.Api.Services;

/// Stand-in for ICrmSyncService in InquiryService and controller tests.
public sealed class FakeCrmSyncService : ICrmSyncService
{
    public List<int> SyncedInquiryIds { get; } = new();
    public CrmSyncResult? RetryResult { get; set; }
    public IReadOnlyList<CrmSyncAttemptResponse>? Attempts { get; set; }

    public Task<CrmSyncResult> SyncInquiryAsync(Inquiry inquiry, CancellationToken ct = default)
    {
        SyncedInquiryIds.Add(inquiry.Id);
        return Task.FromResult(CrmSyncResult.Succeeded("CRM-TEST"));
    }

    public Task<CrmSyncResult?> RetrySyncAsync(int inquiryId, CancellationToken ct = default) =>
        Task.FromResult(RetryResult);

    public Task<IReadOnlyList<CrmSyncAttemptResponse>?> GetAttemptsAsync(int inquiryId, CancellationToken ct = default) =>
        Task.FromResult(Attempts);
}
