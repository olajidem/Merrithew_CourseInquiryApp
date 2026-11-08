namespace CourseInquiry.Api.Integrations.Crm;

public class CrmOptions
{
    public const string SectionName = "Crm";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;        // never log this
    public int MaxAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 250;          // 250ms, 500ms, 1s...
    public double SimulatedFailureRate { get; set; } = 0.2;   // mock only: 0.0 - 1.0
}
