namespace CourseInquiry.Api.Integrations.Crm;

using CourseInquiry.Api.Models;

/// The shape the external CRM expects. Contains PII - never log this object's fields.
public sealed class CrmInquiryPayload
{
    public string ExternalId { get; init; } = string.Empty;
    public string Source { get; init; } = "CourseInquiryApp";
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string CourseName { get; init; } = string.Empty;
    public string? PreferredLocation { get; init; }
    public string? Notes { get; init; }
    public DateTime SubmittedAtUtc { get; init; }

    public static CrmInquiryPayload FromInquiry(Inquiry inquiry) => new()
    {
        ExternalId = inquiry.Id.ToString(),
        FirstName = inquiry.FirstName,
        LastName = inquiry.LastName,
        Email = inquiry.Email,
        Phone = inquiry.Phone,
        CourseName = inquiry.CourseName,
        PreferredLocation = inquiry.PreferredLocation,
        Notes = inquiry.Message,
        SubmittedAtUtc = DateTime.SpecifyKind(inquiry.CreatedDate, DateTimeKind.Utc)
    };

    // Safety net: if someone accidentally logs the payload, no PII leaks.
    public override string ToString() =>
        $"CrmInquiryPayload {{ ExternalId = {ExternalId}, Source = {Source}, Fields = [REDACTED] }}";
}
