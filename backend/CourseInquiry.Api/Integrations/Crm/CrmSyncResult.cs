namespace CourseInquiry.Api.Integrations.Crm;

public sealed class CrmSyncResult
{
    public bool Success { get; private init; }
    public string? CrmReference { get; private init; }
    public string? ErrorMessage { get; private init; }

    /// True = worth retrying (timeout, 503). False = permanent (e.g. validation rejected).
    public bool IsTransient { get; private init; }

    public static CrmSyncResult Succeeded(string crmReference) =>
        new() { Success = true, CrmReference = crmReference };

    public static CrmSyncResult Failed(string errorMessage, bool isTransient) =>
        new() { Success = false, ErrorMessage = errorMessage, IsTransient = isTransient };
}
