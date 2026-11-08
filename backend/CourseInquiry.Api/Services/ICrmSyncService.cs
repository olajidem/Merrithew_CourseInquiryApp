namespace CourseInquiry.Api.Services;

using CourseInquiry.Api.DTOs;
using CourseInquiry.Api.Integrations.Crm;
using CourseInquiry.Api.Models;

public interface ICrmSyncService
{
    /// Sends the inquiry to the CRM with retries. Never throws - failures are recorded and returned.
    Task<CrmSyncResult> SyncInquiryAsync(Inquiry inquiry, CancellationToken ct = default);

    /// Manual retry. Returns null if the inquiry doesn't exist (or is archived).
    Task<CrmSyncResult?> RetrySyncAsync(int inquiryId, CancellationToken ct = default);

    /// Attempt history. Returns null if the inquiry doesn't exist (or is archived).
    Task<IReadOnlyList<CrmSyncAttemptResponse>?> GetAttemptsAsync(int inquiryId, CancellationToken ct = default);
}
