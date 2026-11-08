namespace CourseInquiry.Api.DTOs;

public class CrmSyncAttemptResponse
{
    public int AttemptNumber { get; set; }
    public bool Succeeded { get; set; }
    public bool IsTransientFailure { get; set; }
    public string? CrmReference { get; set; }
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }
    public DateTime AttemptedAt { get; set; }
}
