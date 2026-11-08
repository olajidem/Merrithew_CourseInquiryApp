namespace CourseInquiry.Api.Controllers;

using CourseInquiry.Api.Models;
using CourseInquiry.Api.DTOs;
using CourseInquiry.Api.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class InquiriesController : ControllerBase
{
    private readonly IInquiryService _inquiryService;
    private readonly ICrmSyncService _crmSync;

    public InquiriesController(IInquiryService inquiryService, ICrmSyncService crmSync)
    {
        _inquiryService = inquiryService;
        _crmSync = crmSync;
    }


    // POST api/inquiries Create new inquiry
    [HttpPost]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InquiryResponse>> Create(
        [FromBody] CreateInquiryRequest request,
        CancellationToken ct)
    {
        // [ApiController] automatically returns 400 if the request fails DataAnnotations validation
        var response = await _inquiryService.CreateInquiryAsync(request, ct);

        //return Created($"/api/inquiries/{response.Id}", response);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);

    }

    // GET api/inquiries -> all. Return all inquiries; include optional status filtering
    // GET api/inquiries?status=New -> filtered
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InquiryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<InquiryResponse>>> GetAll(
        [FromQuery] string? status,
        CancellationToken ct)
    {
        InquiryStatus? parsed = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseStatus(status, out var value))
            {
                ModelState.AddModelError(nameof(status), InvalidStatusMessage(status));
                return ValidationProblem(ModelState);
            }
            parsed = value;
        }


        var inquiries = await _inquiryService.GetInquiriesAsync(parsed, ct);
        return Ok(inquiries);
    }

    // GET api/inquiries/5  Return one inquiry by ID
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InquiryResponse>> GetById(int id, CancellationToken ct)
    {
        var inquiry = await _inquiryService.GetInquiryByIdAsync(id, ct);
        return inquiry is null ? InquiryNotFound(id) : Ok(inquiry);
    }

    // Put api/inquiries/5/status   body: { "status": "Contacted" } Update inquiry status
    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InquiryResponse>> UpdateStatus(
        int id,
        [FromBody] UpdateInquiryStatusRequest request,
        CancellationToken ct)
    {
        if (!TryParseStatus(request.Status, out var status))
        {
            ModelState.AddModelError(nameof(request.Status), InvalidStatusMessage(request.Status));
            return ValidationProblem(ModelState);
        }

        var updated = await _inquiryService.UpdateStatusAsync(id, status, ct);
        return updated is null ? InquiryNotFound(id) : Ok(updated);
    }

    // DELETE api/inquiries/5  -> soft delete (archive)
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int id, CancellationToken ct)
    {
        var archived = await _inquiryService.ArchiveInquiryAsync(id, ct);
        return archived ? NoContent() : InquiryNotFound(id);
    }

    // GET api/inquiries/5/crm-sync  -> history of CRM sync attempts
    [HttpGet("{id:int}/crm-sync")]
    [ProducesResponseType(typeof(IReadOnlyList<CrmSyncAttemptResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CrmSyncAttemptResponse>>> GetCrmSyncAttempts(int id, CancellationToken ct)
    {
        var attempts = await _crmSync.GetAttemptsAsync(id, ct);
        return attempts is null ? InquiryNotFound(id) : Ok(attempts);
    }

    // POST api/inquiries/5/crm-sync  -> manual retry
    [HttpPost("{id:int}/crm-sync")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> RetryCrmSync(int id, CancellationToken ct)
    {
        var result = await _crmSync.RetrySyncAsync(id, ct);
        if (result is null) return InquiryNotFound(id);

        return result.Success
            ? Ok(new { message = "Inquiry synced to CRM.", crmReference = result.CrmReference })
            : Problem(statusCode: StatusCodes.Status502BadGateway,
                      title: "CRM sync failed",
                      detail: result.ErrorMessage);
    }



    private ObjectResult InquiryNotFound(int id) =>
        Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Inquiry not found",
                detail: $"No inquiry exists with ID {id}.");

    private static bool TryParseStatus(string? value, out InquiryStatus status) =>
        Enum.TryParse(value, ignoreCase: true, out status) && Enum.IsDefined(status);

    private static string InvalidStatusMessage(string? value) =>
        $"Invalid status '{value}'. Allowed: {string.Join(", ", Enum.GetNames<InquiryStatus>())}.";


}
