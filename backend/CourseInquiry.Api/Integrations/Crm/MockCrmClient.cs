namespace CourseInquiry.Api.Integrations.Crm;

using Microsoft.Extensions.Options;

/// Simulates an external CRM. No network calls, no real API keys.
///
/// Demo triggers (put these in the email address when submitting):
///   jane+crmreject@example.com  -> permanent rejection (no retry)
///   jane+crmdown@example.com    -> CRM "outage" on every attempt (retries, then fails)
/// Otherwise: succeeds, except for a random SimulatedFailureRate of transient errors.
public class MockCrmClient : ICrmClient
{
    private readonly CrmOptions _options;
    private readonly ILogger<MockCrmClient> _logger;

    public MockCrmClient(IOptions<CrmOptions> options, ILogger<MockCrmClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CrmSyncResult> SendInquiryAsync(CrmInquiryPayload payload, CancellationToken ct = default)
    {
        // Simulate network latency
        await Task.Delay(Random.Shared.Next(100, 400), ct);

        // Only non-sensitive identifiers in logs
        _logger.LogDebug("Mock CRM: received inquiry {ExternalId}", payload.ExternalId);

        if (payload.Email.Contains("+crmreject", StringComparison.OrdinalIgnoreCase))
            return CrmSyncResult.Failed("CRM rejected the record (validation error).", isTransient: false);

        if (payload.Email.Contains("+crmdown", StringComparison.OrdinalIgnoreCase))
            throw new HttpRequestException("Simulated CRM outage (503 Service Unavailable).");

        if (Random.Shared.NextDouble() < _options.SimulatedFailureRate)
            throw new HttpRequestException("Simulated transient CRM error (503).");

        var reference = "CRM-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return CrmSyncResult.Succeeded(reference);
    }
}
