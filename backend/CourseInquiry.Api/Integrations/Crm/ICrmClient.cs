namespace CourseInquiry.Api.Integrations.Crm;

public interface ICrmClient
{
    /// Returns a result for business outcomes (accepted / rejected).
    /// May throw HttpRequestException / TimeoutException for transport failures.
    Task<CrmSyncResult> SendInquiryAsync(CrmInquiryPayload payload, CancellationToken ct = default);
}
