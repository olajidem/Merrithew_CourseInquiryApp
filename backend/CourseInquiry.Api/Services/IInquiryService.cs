namespace CourseInquiry.Api.Services;

using CourseInquiry.Api.Models;
using CourseInquiry.Api.DTOs;

public interface IInquiryService
{
    Task<InquiryResponse> CreateInquiryAsync(CreateInquiryRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<InquiryResponse>> GetInquiriesAsync(InquiryStatus? status = null, CancellationToken ct = default);
    Task<InquiryResponse?> GetInquiryByIdAsync(int id, CancellationToken ct = default);
    Task<InquiryResponse?> UpdateStatusAsync(int id, InquiryStatus status, CancellationToken ct = default);

    /// Soft-deletes an inquiry. Returns false if it doesn't exist or is already archived.
    Task<bool> ArchiveInquiryAsync(int id, CancellationToken ct = default);

}
