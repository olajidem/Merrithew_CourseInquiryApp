namespace CourseInquiry.Api.Services;

using Microsoft.EntityFrameworkCore;
using CourseInquiry.Api.DTOs;
using CourseInquiry.Api.Models;
using CourseInquiry.Api.Data;

public class InquiryService : IInquiryService
{
    private readonly AppDbContext _db;
    private readonly ICrmSyncService _crmSync;

    public InquiryService(AppDbContext db, ICrmSyncService crmSync)
    {
        _db = db;
        _crmSync = crmSync;
    }

    public async Task<InquiryResponse> CreateInquiryAsync(CreateInquiryRequest request, CancellationToken ct = default)
    {
        var inquiry = new Inquiry
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Phone = NullIfWhiteSpace(request.Phone),
            CourseName = request.CourseName.Trim(),
            PreferredLocation = NullIfWhiteSpace(request.PreferredLocation),
            Message = NullIfWhiteSpace(request.Message),
            Status = InquiryStatus.New.ToString(),
            CreatedDate = DateTime.UtcNow
        };
        _db.Inquiries.Add(inquiry);
        await _db.SaveChangesAsync(ct);

        // The inquiry is already saved. The CRM outcome is recorded but never fails the request.
        await _crmSync.SyncInquiryAsync(inquiry, ct);

        return MapToResponse(inquiry);
    }

    // Optional fields: store null instead of "" or "   "
    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<IReadOnlyList<InquiryResponse>> GetInquiriesAsync(InquiryStatus? status = null, CancellationToken ct = default)
    {
        var query = _db.Inquiries.AsNoTracking();

        if (status.HasValue)
        {
            var statusName = status.Value.ToString();   // Status is stored as a string, e.g. "New"
            query = query.Where(i => i.Status == statusName);
        }

        var inquiries = await query
            .OrderByDescending(i => i.CreatedDate)
            .ToListAsync(ct);

        return inquiries.Select(MapToResponse).ToList();
    }

    public async Task<InquiryResponse?> GetInquiryByIdAsync(int id, CancellationToken ct = default)
    {
        var inquiry = await _db.Inquiries
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        return inquiry is null ? null : MapToResponse(inquiry);
    }

    public async Task<InquiryResponse?> UpdateStatusAsync(int id, InquiryStatus status, CancellationToken ct = default)
    {
        var inquiry = await _db.Inquiries.FirstOrDefaultAsync(i => i.Id == id, ct);   // not FindAsync: it skips the archive filter for tracked entities

        if (inquiry is null) return null;

        var newStatus = status.ToString();
        if (inquiry.Status != newStatus)          // only write if something actually changed
        {
            inquiry.Status = newStatus;
            inquiry.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return MapToResponse(inquiry);
    }

    public async Task<bool> ArchiveInquiryAsync(int id, CancellationToken ct = default)
    {
        // The query filter hides already-archived rows, so archiving twice returns false
        var inquiry = await _db.Inquiries.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (inquiry is null) return false;

        var now = DateTime.UtcNow;
        inquiry.ArchivedDate = now;
        inquiry.UpdatedDate = now;
        await _db.SaveChangesAsync(ct);
        return true;
    }




    public InquiryResponse MapToResponse(Inquiry inquiry)
    {
        return new InquiryResponse
        {
            Id = inquiry.Id,
            FirstName = inquiry.FirstName,
            LastName = inquiry.LastName,
            Email = inquiry.Email,
            Phone = inquiry.Phone,
            CourseName = inquiry.CourseName,
            PreferredLocation = inquiry.PreferredLocation,
            Message = inquiry.Message,
            Status = inquiry.Status,
            CreatedDate = DateTime.SpecifyKind(inquiry.CreatedDate, DateTimeKind.Utc),
            UpdatedDate = inquiry.UpdatedDate.HasValue
    ? DateTime.SpecifyKind(inquiry.UpdatedDate.Value, DateTimeKind.Utc)
    : null

        };
    }


}
