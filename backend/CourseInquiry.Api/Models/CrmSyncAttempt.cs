namespace CourseInquiry.Api.Models;

using System.ComponentModel.DataAnnotations;

/// One row per attempt to send an inquiry to the CRM (audit trail).
public class CrmSyncAttempt
{
    public int Id { get; set; }

    public int InquiryId { get; set; }

    public int AttemptNumber { get; set; }
    public bool Succeeded { get; set; }
    public bool IsTransientFailure { get; set; }

    [MaxLength(50)]
    public string? CrmReference { get; set; }

    // Our own sanitised message - never raw payload data or exception text
    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public int DurationMs { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
}
